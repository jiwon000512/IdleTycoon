using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// 테스트가 Unity 프로젝트 폴더 기준 경로(Assets/Resources/...)로 파일을 읽으므로, Unity처럼 작업 폴더를 프로젝트 폴더로 맞춘다
[SetUpFixture]
public sealed class UnityProjectRoot
{
    [OneTimeSetUp]
    public void SetRoot()
    {
        string root = typeof(UnityProjectRoot).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == "UnityProject").Value;
        Directory.SetCurrentDirectory(Path.GetFullPath(root));
    }
}
