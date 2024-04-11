
namespace BuildScripts;

[TaskName("BuildTool")]
public class BuildToolTask : FrostingTask { }

[TaskName("TestTool")]
[IsDependentOn(typeof(TestWindowsTask))]
[IsDependentOn(typeof(TestMacOSTask))]
[IsDependentOn(typeof(TestLinuxTask))]
public class TestToolTask : FrostingTask { }

[TaskName("Default")]
[IsDependentOn(typeof(BuildToolTask))]
[IsDependentOn(typeof(TestToolTask))]
public class DefaultTask : FrostingTask { }