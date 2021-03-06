using System;
using System.Collections.Generic;
using FubuCore;
using FubuTestingSupport;
using NUnit.Framework;
using Rhino.Mocks;
using ripple.Model;
using ripple.Nuget;

namespace ripple.Testing.Model
{
	[TestFixture]
	public class SolutionBuilderTester
	{
		private SolutionBuilder theSolutionBuilder;
		private StubSolutionFiles theSolutionFiles;
		private IProjectReader theProjectReader;
		private Solution theSolution;

		private Project p1;
		private Project p2;

		[SetUp]
		public void SetUp()
		{
			theSolutionFiles = new StubSolutionFiles();
			theSolutionFiles.TheSolutionIs(new Solution { SourceFolder = "src", Directory = "."});
			theSolutionFiles.AddProject("Project1.csproj");
			theSolutionFiles.AddProject("Project2.csproj");

			p1 = new Project("Project1.csproj");
			p2 = new Project("Project2.csproj");

			theProjectReader = MockRepository.GenerateStub<IProjectReader>();
			theProjectReader.Stub(x => x.Read("Project1.csproj")).Return(p1);
			theProjectReader.Stub(x => x.Read("Project2.csproj")).Return(p2);

			theSolutionBuilder = new SolutionBuilder(theSolutionFiles, theProjectReader);
			theSolution = theSolutionBuilder.Build();
		}

		[Test]
		public void reads_the_projects()
		{
			theSolution.Projects.ShouldHaveTheSameElementsAs(p1, p2);
		}

		[Test]
		public void sets_the_storage()
		{
			theSolution.Storage.ShouldBeOfType<NugetStorage>();
		}

		[Test]
		public void creates_the_packages_directory()
		{
			new FileSystem().DirectoryExists(theSolution.PackagesDirectory()).ShouldBeTrue();
		}
	}

	public class StubSolutionFiles : ISolutionFiles
	{
		private readonly IList<string> _projects = new List<string>();
		private Solution _solution;

		public string RootDir { get; set; }
		public string BuildSupportDir { get; set; }

		public void AddProject(string project)
		{
			_projects.Fill(project);
		}

		public void TheSolutionIs(Solution solution)
		{
			_solution = solution;
		}

		public void ForProjects(Solution solution, Action<string> action)
		{
			_projects.Each(action);
		}

		public void ForNuspecs(Solution solution, Action<string> action)
		{
		}

		public Solution LoadSolution()
		{
			return _solution;
		}

		public void FinalizeSolution(Solution solution)
		{
		}
	}
}