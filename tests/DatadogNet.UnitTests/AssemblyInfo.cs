using Xunit;

// xunit runs test classes in parallel by default, and this suite cannot tolerate that: the thing
// under test is a static façade. ConfigurationValidationTests calls Datadog.Initialize and
// Datadog.Stop, which set and clear Datadog.Configuration; NeutralHeadContractTests asserts that
// Datadog.Rum.IsEnabled is false, which reads it. Run concurrently, the second sees the first's
// configuration and fails - intermittently, which is the worst way for a test to be wrong.
//
// Serialising costs nothing measurable. The whole suite runs in about 15 milliseconds.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
