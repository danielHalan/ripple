using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FubuCore;
using ripple.Nuget;

namespace ripple.Model
{
	public class FeedService : IFeedService
	{
        private readonly IList<INugetFeed> _offline = new List<INugetFeed>(); 

        private void markOffline(INugetFeed feed)
        {
            _offline.Fill(feed);
        }

        private bool isOffline(INugetFeed feed)
        {
            return _offline.Contains(feed);
        }

        private void tryFeed(INugetFeed feed, Action<INugetFeed> action)
        {
            try
            {
                if (isOffline(feed))
                {
                    RippleLog.Debug("Feed offline. Ignoring.");
                    return;
                }

                action(feed);
            }
            catch (Exception)
            {
                markOffline(feed);
                RippleLog.Debug("Feed unavalable");
            }
        }

		public virtual IRemoteNuget NugetFor(Solution solution, Dependency dependency)
		{
			IRemoteNuget nuget = null;
		    var feeds = feedsFor(solution);
			foreach (var feed in feeds)
			{
                tryFeed(feed, x => nuget = getLatestFromFloatingFeed(x, dependency));
				if (nuget != null) break;

				if (dependency.IsFloat() || dependency.Version.IsEmpty())
				{
				    tryFeed(feed, x => nuget = x.FindLatest(dependency));
					if (nuget != null) break;
				}

                tryFeed(feed, x => nuget = x.Find(dependency));
				if (nuget != null) break;
			}

			if (nuget == null)
			{
                RippleAssert.Fail("Could not find " + dependency);
			}

			return remoteOrCached(solution, nuget);
		}

		private IRemoteNuget remoteOrCached(Solution solution, IRemoteNuget nuget)
		{
			if (nuget == null) return null;
			return solution.Cache.Retrieve(nuget);
		}

		private IRemoteNuget getLatestFromFloatingFeed(INugetFeed feed, Dependency dependency)
		{
			var floatingFeed = feed as IFloatingFeed;
			if (floatingFeed == null) return null;

			var floatedResult = floatingFeed.GetLatest().SingleOrDefault(x => x.Name == dependency.Name);
			if (floatedResult != null && dependency.Mode == UpdateMode.Fixed && floatedResult.IsUpdateFor(dependency))
			{
				return null;
			}

			return floatedResult;
		}

		public IEnumerable<IRemoteNuget> UpdatesFor(Solution solution)
		{
			var nugets = new List<IRemoteNuget>();
			var tasks = solution.Dependencies.Select(dependency => updateNuget(nugets, solution, dependency)).ToArray();

			Task.WaitAll(tasks);

			return nugets;
		}

		public IRemoteNuget UpdateFor(Solution solution, Dependency dependency, bool force = true)
		{
			return LatestFor(solution, dependency, force);
		}

        // Almost entirely covered by integration tests
        public IEnumerable<Dependency> DependenciesFor(Solution solution, Dependency dependency, UpdateMode mode)
        {
            return findDependenciesFor(solution, dependency, mode);
        }

        private IEnumerable<Dependency> findDependenciesFor(Solution solution, Dependency dependency, UpdateMode mode, int depth = 0)
        {
            var nuget = NugetFor(solution, dependency);
            var dependencies = new List<Dependency>();

            if (depth != 0)
            {
                var dep = dependency;
                if (dep.IsFloat() && mode == UpdateMode.Fixed)
                {
                    dep = new Dependency(nuget.Name, nuget.Version, mode);
                }

                dependencies.Add(dep);
            }

            var tasks = new List<Task>();
            nuget
                .Dependencies()
                .Each(x =>
                {
                    var task = Task.Factory.StartNew(() => dependencies.AddRange(findDependenciesFor(solution, x, mode, depth + 1)));
                    tasks.Add(task);
                });

            Task.WaitAll(tasks.ToArray());

            return dependencies.OrderBy(x => x.Name);
        }

		private Task updateNuget(List<IRemoteNuget> nugets, Solution solution, Dependency dependency)
		{
			return Task.Factory.StartNew(() => LatestFor(solution, dependency).CallIfNotNull(nugets.Add));
		}

		public IRemoteNuget LatestFor(Solution solution, Dependency dependency, bool forced = false)
		{
			if (dependency.Mode == UpdateMode.Fixed && !forced)
			{
				return null;
			}

			IRemoteNuget latest = null;
            var feeds = feedsFor(solution);

			foreach (var feed in feeds)
			{
				try
				{
				    IRemoteNuget nuget = null;
                    tryFeed(feed, x => nuget = feed.FindLatest(dependency));
					
					if (latest == null)
					{
						latest = nuget;
					}

					if (latest != null && nuget != null && latest.Version < nuget.Version)
					{
						latest = nuget;
					}
				}
				catch (Exception)
				{
					RippleLog.Debug("Error while finding latest " + dependency);
				}
			}

			if (isUpdate(latest, solution, dependency))
			{
				return remoteOrCached(solution, latest);
			}

			return null;
		}

		private bool isUpdate(IRemoteNuget latest, Solution solution, Dependency dependency)
		{
			if (latest == null) return false;

			var localDependencies = solution.LocalDependencies();
			if (localDependencies.Has(dependency))
			{
				var local = localDependencies.Get(dependency);
				return latest.IsUpdateFor(local);
			}

			if (dependency.IsFloat())
			{
				return true;
			}

			return latest.IsUpdateFor(dependency);
		}

        private IEnumerable<INugetFeed> feedsFor(Solution solution)
        {
            if (!RippleConnection.Connected())
            {
                var cache = solution.Cache.ToFeed();
                return new[] { cache.GetNugetFeed() };
            }

            return solution.Feeds.Select(x => x.GetNugetFeed());
        }
	}
}