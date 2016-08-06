///////////////////////////////////////////////////////////////////////////////
// Directives
///////////////////////////////////////////////////////////////////////////////

#l "tools/versionUtils.cake"
#l "tools/settingsUtils.cake"
#tool "nuget:?package=NUnit.ConsoleRunner"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");
var settingsFile = Argument<string>("settingsFile", ".\\settings.json");
var skipBuild = Argument<string>("skipBuild", "false").ToLower() == "true" || Argument<string>("skipBuild", "false") == "1";

var buildSettings = SettingsUtils.LoadSettings(Context, settingsFile);
var versionInfo = VersionUtils.LoadVersion(Context, buildSettings);

// Allow for any overrides
buildSettings.NuGet.LibraryMinVersionDependency = (Argument<string>("dependencyVersion", buildSettings.NuGet.LibraryMinVersionDependency)).Replace(":",".");
buildSettings.NuGet.VersionDependencyTypeForLibrary = Argument<VersionDependencyTypes>("dependencyType", buildSettings.NuGet.VersionDependencyTypeForLibrary);

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutions = GetFiles(buildSettings.Build.SolutionFilePath);
var solutionPaths = solutions.Select(solution => solution.GetDirectory());

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup((c) =>
{
	c.Information("Command Line:");
	c.Information("\tConfiguration: {0}", configuration);
	c.Information("\tSettings Files: {0}", settingsFile);
	c.Information("\tSkip Build: {0}", skipBuild);
	c.Information("\tSolutions Found: {0}", solutions.Count);

    // Executed BEFORE the first task.
	buildSettings.Display(c);
	versionInfo.Display(c);
});

Teardown((c) =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans all directories that are used during the build process.")
	.WithCriteria(!skipBuild)
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
		try { CleanDirectories(path + "/**/bin/" + configuration); } catch {}
        try { CleanDirectories(path + "/**/obj/" + configuration); } catch {}
    }
});

Task("CleanAll")
    .Description("Cleans all directories that are used during the build process.")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin");
        CleanDirectories(path + "/**/obj");
		CleanDirectories(path + "/packages/**/*");
		CleanDirectories(path + "/artifacts/**/*");
		CleanDirectories(path + "/packages");
		CleanDirectories(path + "/artifacts");
    }
});

Task("CleanPackages")
    .Description("Cleans all packages that are used during the build process.")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
		CleanDirectories(path + "/packages/**/*");
		CleanDirectories(path + "/packages");
    }
});

Task("Restore")
    .Description("Restores all the NuGet packages that are used by the specified solution.")
	.WithCriteria(!skipBuild)
    .Does(() =>
{
    // Restore all NuGet packages.
    foreach(var solution in solutions)
    {
        Information("Restoring {0}...", solution);
        NuGetRestore(solution, new NuGetRestoreSettings { ConfigFile = buildSettings.NuGet.NuGetConfig });
    }
});

Task("Build")
    .Description("Builds all the different parts of the project.")
	.WithCriteria(!skipBuild)
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
	.IsDependentOn("UpdateVersion")
    .Does(() =>
{
	if (buildSettings.Version.AutoIncrementVersion)
	{
		RunTarget("IncrementVersion");
	}

    // Build all solutions.
    foreach(var solution in solutions)
    {
        Information("Building {0}", solution);
        MSBuild(solution, settings =>
            settings.SetPlatformTarget(PlatformTarget.MSIL)
				.SetMaxCpuCount(buildSettings.Build.MaxCpuCount)
                .WithProperty("TreatWarningsAsErrors",buildSettings.Build.TreatWarningsAsErrors.ToString())
                .WithTarget("Build")
                .SetConfiguration(configuration));
    }
});

Task("UnitTest")
	.Description("Run unit tests for the solution.")
	.WithCriteria(!skipBuild)
	.IsDependentOn("Build")
	.Does(() => 
{
    // Run all unit tests we can find.
			
	var assemplyFilePath = string.Format("{0}/**/bin/{1}/{2}", buildSettings.Test.SourcePath, configuration, buildSettings.Test.AssemblyFileSpec);
	
	Information("Unit Test Files: {0}", assemplyFilePath);
	
	var unitTestAssemblies = GetFiles(assemplyFilePath);
	
	foreach(var uta in unitTestAssemblies)
	{
		Information("Executing Tests for {0}", uta);
		
		switch (buildSettings.Test.Framework)
		{
			case TestFrameworkTypes.NUnit2:
				NUnit(uta.ToString(), new NUnitSettings { });
				break;
			case TestFrameworkTypes.NUnit3:
				NUnit3(uta.ToString(), new NUnit3Settings { Configuration=configuration });
				break;
			case TestFrameworkTypes.XUnit:
				XUnit(uta.ToString(), new XUnitSettings { OutputDirectory = buildSettings.Test.ResultsPath });
				break;
			case TestFrameworkTypes.XUnit2:
				XUnit2(uta.ToString(), new XUnit2Settings { OutputDirectory = buildSettings.Test.ResultsPath, XmlReportV1 = true });
				break;
		}
	}
});

Task("Package")
    .Description("Packages all nuspec files into nupkg packages.")
    .IsDependentOn("UnitTest")
    .Does(() =>
{
	var artifactsPath = Directory(buildSettings.NuGet.ArtifactsPath);
	var nugetProps = new Dictionary<string, string>() { {"Configuration", configuration} };
	
	
	CreateDirectory(artifactsPath);
	
	var nuspecFiles = GetFiles(buildSettings.NuGet.NuSpecFileSpec);
	foreach(var nsf in nuspecFiles)
	{
		Information("Packaging {0}", nsf);
		
		if (buildSettings.NuGet.UpdateVersion) {
			VersionUtils.UpdateNuSpecVersion(Context, buildSettings, versionInfo, nsf.ToString());	
		}
		
		if (buildSettings.NuGet.UpdateLibraryDependencies) {
			VersionUtils.UpdateNuSpecVersionDependency(Context, buildSettings, versionInfo, nsf.ToString());
		}
		
		NuGetPack(nsf, new NuGetPackSettings {
			Version = versionInfo.ToString(),
			ReleaseNotes = versionInfo.ReleaseNotes,
			Symbols = true,
			Properties = nugetProps,
			OutputDirectory = artifactsPath
		});
	}
});

Task("Publish")
    .Description("Publishes all of the nupkg packages to the nuget server. ")
    .IsDependentOn("Package")
    .Does(() =>
{
	var nupkgFiles = GetFiles(buildSettings.NuGet.NuGetPackagesSpec);
	foreach(var pkg in nupkgFiles)
	{
		// Lets skip everything except the current version 
		if (!pkg.ToString().Contains(versionInfo.ToString())) {
			Information("Skipping {0}", pkg);
			continue; 
		}
		
		Information("Publishing {0}", pkg);
		
		try
		{
			NuGetPush(pkg, new NuGetPushSettings {
				Source = buildSettings.NuGet.FeedUrl,
				ApiKey = buildSettings.NuGet.FeedApiKey,
				ConfigFile = buildSettings.NuGet.NuGetConfig,
				Verbosity = NuGetVerbosity.Detailed
			});
		}
		catch (Exception ex)
		{
			Information("\tFailed to published: ", ex.Message);
		}
	}
});

Task("UnPublish")
    .Description("UnPublishes all of the current nupkg packages from the nuget server. Issue: versionToDelete must use : instead of . due to bug in cake")
    .Does(() =>
{
	var v = Argument<string>("versionToDelete", versionInfo.ToString()).Replace(":",".");
	
	var nuspecFiles = GetFiles(buildSettings.NuGet.NuSpecFileSpec);
	foreach(var f in nuspecFiles)
	{
		Information("UnPublishing {0}", f.GetFilenameWithoutExtension());

		var args = string.Format("delete {0} {1} -Source {2} -NonInteractive", 
								f.GetFilenameWithoutExtension(),
								v,
								buildSettings.NuGet.FeedUrl
								);
	
		//if (buildSettings.NuGet.FeedApiKey != "VSTS" ) {
			args = args + string.Format(" -ApiKey {0}", buildSettings.NuGet.FeedApiKey);
		//}
				
		if (!string.IsNullOrEmpty(buildSettings.NuGet.NuGetConfig)) {
			args = args + string.Format(" -Config {0}", buildSettings.NuGet.NuGetConfig);
		}
		
		Information("NuGet Command Line: {0}", args);
		using (var process = StartAndReturnProcess("tools\\nuget.exe", new ProcessSettings {
			Arguments = args
		}))
		{
			process.WaitForExit();
			Information("nuget delete exit code: {0}", process.GetExitCode());
		}
	}
});

Task("UpdateVersion")
	.Description("Updates the version number in the necessary files")
	.Does(() =>
{
	Information("Updating Version to {0}", versionInfo.ToString());
	
	VersionUtils.UpdateVersion(Context, buildSettings, versionInfo);
});

Task("IncrementVersion")
	.Description("Increments the version number and then updates it in the necessary files")
	.Does(() =>
{
	var oldVer = versionInfo.ToString();
	if (versionInfo.IsPreRelease) versionInfo.PreRelease++; else versionInfo.Build++;
	
	Information("Incrementing Version {0} to {1}", oldVer, versionInfo.ToString());
	
	RunTarget("UpdateVersion");	
});

Task("BuildNewVersion")
	.Description("Increments and Builds a new version")
	.IsDependentOn("IncrementVersion")
	.IsDependentOn("Build")
	.Does(() =>
{
});

Task("PublishNewVersion")
	.Description("Increments, Builds, and publishes a new version")
	.IsDependentOn("BuildNewVersion")
	.IsDependentOn("Publish")
	.Does(() =>
{
});

Task("DisplaySettings")
    .Description("Displays All Settings.")
    .Does(() =>
{
	// Settings will be displayed as they are part of the Setup task
});

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .Description("This is the default task which will be ran if no specific target is passed in.")
    .IsDependentOn("Build");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);