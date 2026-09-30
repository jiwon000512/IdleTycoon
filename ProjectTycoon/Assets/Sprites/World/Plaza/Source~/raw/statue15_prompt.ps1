$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot
$py = "C:\Users\jiwon\AppData\Local\Programs\Python\Python312\python.exe"
# 결 기준: 지금 석상(60칸)을 한 칸 16px로. 디자인 기준: 처음 받침 B 원본
& $py -c "from PIL import Image; im=Image.open(r'C:/project/Tycoon/ProjectTycoon/Assets/Sprites/World/Plaza/statue.png').convert('RGBA'); c=im.resize((im.width//2,im.height//2),Image.NEAREST); b=c.resize((c.width*16,c.height*16),Image.NEAREST); bg=Image.new('RGBA',(b.width+128,b.height+128),(255,255,255,255)); bg.paste(b,(64,64),b); bg.convert('RGB').save('ref_statue_now16.png')"
$prompt = @'
Use your IMAGE GENERATION tool to paint a brand-new image (do NOT write code, do NOT resample, crop or copy the reference files).
Pixel-art stone wombat statue for a cozy mobile game, on a strict pixel grid, every art pixel a crisp square, about 12 screen pixels per art pixel (the output image should be around 1100 px wide).
Reference image 1 is the CURRENT statue (a round grey stone wombat standing on a drum pedestal), drawn on a grid where one art pixel = 16 screen pixels, 60 art pixels wide. Make the NEW statue 1.5 times bigger in art pixels: the whole statue about 87 art pixels wide and 87 tall, the stone wombat about 63 art pixels wide, with MORE pixel detail than reference 1 (not just scaled up).
Keep the wombat looking exactly like reference 1 (same round body, small round ears, dot eyes, big dark nose, smile, paws on chest, smooth grey stone, one white highlight stroke on the head).
The pedestal must copy reference image 2 faithfully: an oval drum pedestal with a flat light top, a carved decorative band of small arches around its side, and a round stone offering bowl attached at the front center (empty). Keep the carved arches crisp and readable.
Stone palette: soft warm greys, 1-pixel dark brown outline (#342020), one darker tone on the shadow side, no anti-aliasing, no gradients, no dithering. Orthographic three-quarter top-down view (top of the pedestal visible as a flat ellipse band, vertical edges vertical). Pure white background, nothing else, no text, no shadow.
'@
$jobs = @()
foreach ($n in 'st15_a', 'st15_b', 'st15_c') {
  $p = $prompt + "`nSave the image as $n.png in the current working directory and reply with only the path."
  $jobs += Start-Job -ScriptBlock { param($dir, $p, $n) Set-Location $dir; $p | codex exec --skip-git-repo-check -s workspace-write -c model_reasoning_effort=low -i ref_statue_now16.png -i ped_b.png - *> "log_$n.txt" } -ArgumentList $PSScriptRoot, $p, $n
}
$jobs | Wait-Job | Out-Null
Get-ChildItem st15_*.png | ForEach-Object { "{0} {1}" -f $_.Name, $_.Length }
