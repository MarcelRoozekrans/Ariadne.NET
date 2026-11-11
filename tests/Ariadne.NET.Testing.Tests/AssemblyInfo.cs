// The checks load fixture solutions through MSBuildWorkspace, whose design-time build writes under each fixture's obj
// folder, so two loads of one fixture at the same time collide. The tests run one after the other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
