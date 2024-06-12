// Copyright Epic Games, Inc. All Rights Reserved.

using EpicGames.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System;
using UnrealBuildTool;


public class CMakeProject
{
    ReadOnlyTargetRules TargetRules;
    ModuleRules ModuleRules;
    string ProjectName = "";
    string ProjectDir = "";
    string IntermediateDir = "";
    string BuildDir = "";
    string BuiltFile = "";
    string BuildInfoFile = "";

    string BuildType = "";
    string Generator = "";
    string PlatformArgs;
    string CCompiler = "";
    string CXXCompiler = "";
    string Linker = "";
    string AdditionalArgs = "";

    int SyncExecuteCommand(string command)
    {
        ProcessStartInfo processInfo = null;
        if (BuildHostPlatform.Current.Platform == UnrealTargetPlatform.Win64)
        {
            command = "/c " + command;
            Console.WriteLine("[ExecuteCommand] cmd.exe " + command);
            processInfo = new ProcessStartInfo("cmd.exe", command);
        }
        else if (IsUnixPlatform(BuildHostPlatform.Current.Platform))
        {
            command = "-c  \"" + command.Replace("\"", "\\\"") + " \"";
            Console.WriteLine("[ExecuteCommand] bash " + command);
            processInfo = new ProcessStartInfo("bash", command);
        }

        processInfo.CreateNoWindow = true;
        processInfo.UseShellExecute = false;
        processInfo.RedirectStandardError = true;
        processInfo.RedirectStandardOutput = true;
        processInfo.WorkingDirectory = Path.GetFullPath(ModuleRules.ModuleDirectory);

        StringBuilder outputString = new StringBuilder();
        Process p = Process.Start(processInfo);

        p.OutputDataReceived += (sender, args) => { outputString.Append(args.Data); Console.WriteLine(args.Data); };
        p.ErrorDataReceived += (sender, args) => { outputString.Append(args.Data); Console.WriteLine(args.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();

        if (p.ExitCode != 0)
        {
            Console.WriteLine(outputString);
        }
        return p.ExitCode;
    }

    public CMakeProject(ReadOnlyTargetRules targetRules, ModuleRules moduleRules, string projectName, string projectPath, string additionalArgs)
    {
        TargetRules = targetRules;
        ModuleRules = moduleRules;
        ProjectName = projectName;
        ProjectDir = projectPath;

        if (targetRules.Platform == UnrealTargetPlatform.Win64)
        {
            switch (targetRules.WindowsPlatform.Compiler)
            {
                case WindowsCompiler.Clang:
                    Generator = "NMake Makefiles";
                    break;
                case WindowsCompiler.Intel:
                    Generator = "NMake Makefiles";
                    break;
                case WindowsCompiler.VisualStudio2019:
                    Generator = "Visual Studio 16 2019";
                    break;
                case WindowsCompiler.VisualStudio2022:
                    Generator = "Visual Studio 17 2022";
                    if (targetRules.WindowsPlatform.Architecture == UnrealArch.X64)
                    {
                        PlatformArgs = "-A x64";
                    }
                    else if (targetRules.WindowsPlatform.Architecture == UnrealArch.Arm64)
                    {
                        PlatformArgs = "-A ARM64";
                    }
                    break;
                default:
                    break;
            }
            PlatformArgs += " -T host=x64";
        }
        else if (IsUnixPlatform(targetRules.Platform))
        {
            Generator = "Unix Makefiles";
            UEBuildPlatformSDK buildPlatformSDK = UEBuildPlatformSDK.GetSDKForPlatform(targetRules.Platform.ToString());
            if (buildPlatformSDK != null)
            {
                string internalSDKPath = buildPlatformSDK.GetInternalSDKPath();

                if (!string.IsNullOrEmpty(internalSDKPath))
                {
                    CCompiler = Path.Combine(internalSDKPath, "bin", "clang");
                    CXXCompiler = Path.Combine(internalSDKPath, "bin", "clang++");
                    Linker = Path.Combine(internalSDKPath, "bin", "lld");
                }
            }
        }

        if (targetRules.Configuration == UnrealTargetConfiguration.Debug)
        {
            BuildType = "Debug";
        }
        else
        {
            BuildType = "Release";
        }

        IntermediateDir = Path.Combine(moduleRules.Target.ProjectFile.Directory.FullName, "Intermediate", "CMakeTarget", ProjectName);
        BuildDir = Path.Combine(IntermediateDir, "build");
        BuiltFile = Path.Combine(IntermediateDir, BuildType + ".built");
        BuildInfoFile = Path.Combine(BuildDir, "buildinfo_" + BuildType + ".output").Replace("\\", "/");
        AdditionalArgs = additionalArgs;
    }

    public void Build()
    {
        bool configCMake = true;
        string rootCMakeLists = Path.GetFullPath(Path.Combine(ProjectDir, "CMakeLists.txt"));
        string cmakeExecutable = BuildHostPlatform.Current.Platform == UnrealTargetPlatform.Win64 ? "cmake.exe" : "cmake";
        if (File.Exists(BuiltFile))
        {
            DateTime cmakelistLastWrite = File.GetLastWriteTime(rootCMakeLists);
            string builtTimeString = System.IO.File.ReadAllText(BuiltFile);
            DateTime builtTime = DateTime.Parse(builtTimeString);

            if (builtTime.EqualsUpToSeconds(cmakelistLastWrite))
                configCMake = false;
        }
        if (configCMake)
        {
            string cmakeArgs = " -G \"" + Generator + "\"" +
                " -S \"" + ProjectDir + "\"" +
                " -B \"" + BuildDir + "\"" +
                " -DCMAKE_BUILD_TYPE=" + BuildType +
                " -DCMAKE_INSTALL_PREFIX=\"" + ProjectDir + "\"";
            if (!String.IsNullOrEmpty(CCompiler))
                cmakeArgs += " -DCMAKE_C_COMPILER=" + CCompiler;
            if (!String.IsNullOrEmpty(CXXCompiler))
                cmakeArgs += " -DCMAKE_CXX_COMPILER=" + CXXCompiler;

            cmakeArgs += " " + PlatformArgs;
            cmakeArgs += " " + AdditionalArgs;

            string cmakeCommand = cmakeExecutable + cmakeArgs;
            var configureCode = SyncExecuteCommand(cmakeCommand);
            if (configureCode != 0)
            {
                throw new BuildException("CMake configure failed with code: {0}", configureCode);
            }
        }


        string buildCommand = cmakeExecutable + " --build \"" + BuildDir + "\" --config " + BuildType;
        var buildCode = SyncExecuteCommand(buildCommand);
        if (buildCode == 0)
        {
            if (configCMake)
            {
                DateTime cmakeLastWrite = File.GetLastWriteTime(rootCMakeLists);
                File.WriteAllText(BuiltFile, cmakeLastWrite.ToString());
            }
        }
        else
        {
            throw new BuildException("CMake build failed with code: {0}", buildCode);
        }

    }

    public void Link()
    {
        Console.WriteLine("Loading build info file: " + BuildInfoFile);

        if (!File.Exists(BuildInfoFile))
        {
            throw new BuildException("BuildInfoFile[{0}] dont exist", BuildInfoFile);
        }

        Dictionary<string, string> values = new Dictionary<string, string>();

        StreamReader reader = new System.IO.StreamReader(BuildInfoFile);
        string line = null;

        while ((line = reader.ReadLine()) != null)
        {
            string[] tokens = line.Split('=');

            if (tokens.Length != 2)
                continue;

            values.Add(tokens[0], tokens[1]);
        }

        if (values.ContainsKey("cppStandard"))
        {
            string standard = values["cppStandard"];

            if (!String.IsNullOrEmpty(standard))
            {
                if (standard.Equals("11"))
                    ModuleRules.CppStandard = CppStandardVersion.Default;
                else if (standard.Equals("14"))
                    ModuleRules.CppStandard = CppStandardVersion.Cpp14;
                else if (standard.Equals("17"))
                    ModuleRules.CppStandard = CppStandardVersion.Cpp17;
                else if (standard.Equals("20"))
                    ModuleRules.CppStandard = CppStandardVersion.Cpp20;
                else
                    ModuleRules.CppStandard = CppStandardVersion.Latest;

                //                    if (ModuleRules.Target.Platform == UnrealTargetPlatform.Linux)
                //                        ModuleRules.PublicSystemLibraries.Add("stdc++"); //only include if using included compiler and linux  
            }
        }

        if (values.ContainsKey("dependencies"))
        {
            string[] dependencies = values["dependencies"].Split(',');

            foreach (string depend in dependencies)
            {
                if (String.IsNullOrEmpty(depend))
                    continue;

                ModuleRules.ExternalDependencies.Add(depend);
            }
        }

        if (values.ContainsKey("sourceDependencies"))
        {
            string sourcePath = "";

            if (values.ContainsKey("sourcePath"))
                sourcePath = values["sourcePath"];

            string[] dependencies = values["sourceDependencies"].Split(',');

            foreach (string depend in dependencies)
            {
                if (String.IsNullOrEmpty(depend))
                    continue;

                string dependPath = Path.Combine(sourcePath, depend);

                ModuleRules.ExternalDependencies.Add(dependPath);
            }
        }

        if (values.ContainsKey("includes"))
        {
            string[] includes = values["includes"].Split(',');

            foreach (string include in includes)
            {
                if (String.IsNullOrEmpty(include))
                    continue;

                ModuleRules.PublicIncludePaths.Add(include);
            }
        }

        if (values.ContainsKey("binaryDirectories"))
        {
            string[] binaryDirectories = values["binaryDirectories"].Split(',');

            foreach (string binaryDirectory in binaryDirectories)
            {
                if (String.IsNullOrEmpty(binaryDirectory))
                    continue;

                Console.WriteLine("Add library path: " + binaryDirectory);
                ModuleRules.PublicRuntimeLibraryPaths.Add(binaryDirectory);
            }
        }

        if (values.ContainsKey("libraries"))
        {
            string[] libraries = values["libraries"].Split(',');

            foreach (string library in libraries)
            {
                if (String.IsNullOrEmpty(library))
                    continue;

                ModuleRules.PublicAdditionalLibraries.Add(library);
            }
        }
    }

    private static bool IsUnixPlatform(UnrealTargetPlatform platform)
    {
        return platform == UnrealTargetPlatform.Linux || platform == UnrealTargetPlatform.Mac;
    }
}

public class ExternalCMakeProject : ModuleRules
{
	public ExternalCMakeProject(ReadOnlyTargetRules Target) : base(Target)
	{
		PublicDependencyModuleNames.AddRange(
			new string[]
			{
				"Core",
				// ... add other public dependencies that you statically link with here ...
			}
			);


        PrivateDependencyModuleNames.AddRange(
            new string[]
            {
                "CoreUObject",
                "Engine",
            }
            );
    }

    public static void Add(ReadOnlyTargetRules targetRules, ModuleRules moduleRules, string projectName, string projectPath, string cmakeArgs)
    {
        CMakeProject cmakeProject = new CMakeProject(targetRules, moduleRules, projectName, projectPath, cmakeArgs);
        cmakeProject.Build();
        cmakeProject.Link();
    }

}
