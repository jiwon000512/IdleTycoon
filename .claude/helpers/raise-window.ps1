# 사용자가 볼 일이 있을 때만 그 세션의 터미널 창을 화면 맨 앞으로 올린다(.claude/settings.local.json 훅, 2026-10-01 사용자 「개입할 때 · 끝났을 때 한 번씩만」).
#   AskUserQuestion(PreToolUse) · 권한 확인(Notification permission_prompt) · 사용자가 시킨 일의 끝(Stop). Stop을 건너뛰는 때(다른 방 메시지 턴 · 백그라운드를 기다리는 멈춤)는
#   토스트(~/.claude/helpers/notify.ps1)와 같은 판정 ~/.claude/helpers/alert-filter.ps1(2026-10-06 사용자 「선택·의견이 필요할 때와 다 끝나 멈췄을 때만」).
# 훅 프로세스는 콘솔이 없어서, 부모를 거슬러 claude.exe를 찾아 그 콘솔에 잠깐 붙고 콘솔 창의 주인(Windows Terminal 창)을 찾는다.
# Windows는 백그라운드 프로세스의 SetForegroundWindow를 막으므로 Alt를 한 번 눌렀다 떼고 부른다.
$hook = $null
try { $hook = [Console]::In.ReadToEnd() | ConvertFrom-Json } catch {}
. "$env:USERPROFILE\.claude\helpers\alert-filter.ps1"
if ($hook -and -not (Test-ShouldAlert $hook)) { "{0:HH:mm:ss} skip" -f (Get-Date) | Out-File -Encoding utf8 "$env:TEMP\raise-window.log"; exit 0 }
Add-Type -Namespace Raise -Name U -MemberDefinition @'
[DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
[DllImport("kernel32.dll")] public static extern bool AttachConsole(uint pid);
[DllImport("kernel32.dll")] public static extern bool FreeConsole();
[DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
[DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
[DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
[DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
'@

$p = Get-CimInstance Win32_Process -Filter "ProcessId=$PID"
while ($p -and $p.Name -ne 'claude.exe') { $p = Get-CimInstance Win32_Process -Filter ("ProcessId=" + $p.ParentProcessId) }
if (-not $p) { exit 0 }

[void][Raise.U]::FreeConsole()
if (-not [Raise.U]::AttachConsole([uint32]$p.ProcessId)) { exit 0 }
$window = [Raise.U]::GetAncestor([Raise.U]::GetConsoleWindow(), 3)   # GA_ROOTOWNER: 의사 콘솔 → 터미널 창
[void][Raise.U]::FreeConsole()
if ($window -eq [IntPtr]::Zero) { exit 0 }

if ([Raise.U]::IsIconic($window)) { [void][Raise.U]::ShowWindow($window, 9) }   # SW_RESTORE
[Raise.U]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)   # Alt 누름
[Raise.U]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)   # Alt 뗌
# 지금 맨 앞 창의 입력 스레드에 잠깐 붙어야 앞으로 가져오기가 막히지 않는다
$front = [Raise.U]::GetWindowThreadProcessId([Raise.U]::GetForegroundWindow(), [IntPtr]::Zero)
$me = [Raise.U]::GetCurrentThreadId()
$joined = ($front -ne 0 -and $front -ne $me) -and [Raise.U]::AttachThreadInput($me, $front, $true)
[void][Raise.U]::BringWindowToTop($window)
$ok = [Raise.U]::SetForegroundWindow($window)
if ($joined) { [void][Raise.U]::AttachThreadInput($me, $front, $false) }
# 마지막 한 번의 결과(창이 안 올라올 때 원인 보기용, 덮어씀)
"{0:HH:mm:ss} target={1} set={2} foreground={3}" -f (Get-Date), $window, $ok, [Raise.U]::GetForegroundWindow() | Out-File -Encoding utf8 "$env:TEMP\raise-window.log"
