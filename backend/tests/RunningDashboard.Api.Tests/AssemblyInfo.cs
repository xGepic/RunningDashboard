// Each test gets a fresh instance of its class, so no TestApp is shared unless a class opts in; classes run in parallel.
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[assembly: Parallelizable(ParallelScope.Fixtures)]
