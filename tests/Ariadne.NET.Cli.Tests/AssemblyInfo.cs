// The CLI commands resolve .architecture/ relative to the process working directory,
// so tests that change it must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
