#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.8.0"
#addin nuget:?package=Cake.Sonar&version=1.1.0

#tool "nuget:?package=NuGet.CommandLine"
#addin nuget:?package=Cake.Compression
#addin nuget:?package=Cake.Docker&version=1.1.0
#addin nuget:?package=Cake.FileHelpers&version=4.0.1
#addin nuget:?package=Cake.SemVer

#addin nuget:?package=semver
#addin nuget:?package=SharpZipLib

#addin "Cake.Npm"

using Semver;
using System.Text.RegularExpressions;

// =========
// ARGUMENTS FROM COMMAND LINE
// =========
var target = Argument("target", "WhatBuildToPutHere");
var configuration = Argument("configuration", "Release");
var environment = Argument("environment", "Development");
var useVersionFromFile = Argument<bool>("useVersionFromFile", true);

// ================
// GLOBAL CONSTANTS
// ================
const string solutionFile = "./EventStore.Replicator.sln";
const string eventReplicatorProjectFile = "./src/es-replicator/es-replicator.csproj";

var buildNumber = "0";
var version = new Semver.SemVersion(1);

Setup(context =>
{
    if (BuildSystem.TeamCity.IsRunningOnTeamCity)
    {
      Information(
          @"Environment:
          PullRequest: {0}
          Build Configuration Name: {1}
          TeamCity Project Name: {2}
          Build Number {3}

          ",
          BuildSystem.TeamCity.Environment.PullRequest.IsPullRequest,
          BuildSystem.TeamCity.Environment.Build.BuildConfName,
          BuildSystem.TeamCity.Environment.Project.Name,
          BuildSystem.TeamCity.Environment.Build.Number
          );


      buildNumber = BuildSystem.TeamCity.Environment.Build.Number;
    }
    else
    {
      Information("Not running on TeamCity");
    }

    Information(
          @"Arguments:
            Target: {0}
            Configuration: {1}
            Environment: {2}
            UseVersionFromFile {3}

            ",
            target,
            configuration,
            environment,
            useVersionFromFile);

	// Do we want to add Version file into Replicator?
    // if (useVersionFromFile)
    // {
        // var fileVersion = FileReadText("./VERSION").Trim();
        // version = ParseSemVer(fileVersion + "." + buildNumber);
    // }
    // else
    // {
        version = ParseSemVer(buildNumber);
    // }

    Information("Running target: {0}", target);

    Information("Build Version: {0} using {1} for config environment {2}", version, configuration, environment);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

Task("Install-Node-Dependencies")
    .Does(() =>
{
    Information("Installing Node.js dependencies...");
	
	var settings = new NpmInstallSettings 
        {
            WorkingDirectory = "./src/es-replicator/ClientApp"
        };
    NpmInstall(settings);
    //NpmInstall("install", "./src/es-replicator/ClientApp");
});

// Build Front-End Assets
Task("Build-Front-End")
    .IsDependentOn("Install-Node-Dependencies")
    .Does(() =>
{
    Information("Building front-end assets...");
    //NpmRunScript("build", "./src/es-replicator/ClientApp");
	
	var settings = 
        new NpmRunScriptSettings 
        {
            ScriptName = "build",
            LogLevel = NpmLogLevel.Verbose,
			WorkingDirectory = "./src/es-replicator/ClientApp"
        };
    NpmRunScript(settings);
});

// Is Clean really needed?
//Task("Clean")
//    .Does(() =>
//        {
//          CleanDirectory(buildDir);
//          CleanDirectory("./artifacts");
//        });

Task("Restore-DotNet-Dependencies")
  // .IsDependentOn("Clean")
  .Does(() =>
        {
            var parsedSolution = ParseSolution(solutionFile);

            foreach(var project in parsedSolution.Projects)
            {
                if(Regex.IsMatch(project.Path.FullPath, "(\\**\\/*.*.[c]sproj$)", RegexOptions.IgnoreCase))
                {
					DotNetRestore(project.Path.FullPath);
                }
            }
        });
		
Task("WhatBuildToPutHere")
	//.IsDependentOn("Install-Node-Dependencies")
	//.IsDependentOn("Build-Front-End")
	.IsDependentOn("Restore-DotNet-Dependencies")
    // .IsDependentOn("Restore")
    // .IsDependentOn("Config")
    .Does(() =>
        {
            var parsedSolution = ParseSolution(solutionFile);

            foreach(var project in parsedSolution.Projects)
            {
                if(Regex.IsMatch(project.Path.FullPath, "(\\**\\/*.*.[fc]sproj$)", RegexOptions.IgnoreCase))
                {
                    // var targetFrameworks = GetProjectTargetFrameworks(project.Path);

					// Information($"Project: '{project.Name}' targets: '{targetFrameworks}' Running: 'DotNetBuild'");
					DotNetBuild(project.Path.FullPath, new DotNetBuildSettings { Configuration = configuration });
             
                }
            }

			// Need to decide to move es-replicator bin to a specific location for Octopus to grab?
            // CreateDirectory(library_packages_dir);
            // CopyFiles($"src/Huddle.Babble.Notifications.Uris/bin/{configuration}/Huddle.Babble.Notifications.Uris.*.nupkg", $"{library_packages_dir}");
        });

// ***************
// COMPOSITE TASKS
// ***************

RunTarget(target);