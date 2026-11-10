using Ariadne.NET.Testing;

// The first argument is a folder with a project and an .architecture folder. Its pack forbids a dependency that the
// project has, so the check must report exactly that violation, and AssertValid must throw with it in the message.
var fixture = args.Length > 0 ? args[0] : throw new ArgumentException("Pass the fixture folder as the first argument.");

var report = ArchitectureCheck.Run(fixture);
Console.WriteLine($"Checked {report.TypesChecked} types: {report.Violations.Count} violations");
if (report.Violations.Count != 1 || report.Violations[0].RuleId != "FIXTURE-DOMAIN-NO-INFRASTRUCTURE")
{
    Console.Error.WriteLine("Expected one violation of FIXTURE-DOMAIN-NO-INFRASTRUCTURE.");
    return 1;
}

try
{
    ArchitectureCheck.AssertValid(fixture);
}
catch (ArchitectureViolationException exception) when (exception.Message.Contains("FIXTURE-DOMAIN-NO-INFRASTRUCTURE", StringComparison.Ordinal))
{
    Console.WriteLine(exception.Message);
    return 0;
}

Console.Error.WriteLine("AssertValid did not throw an ArchitectureViolationException that names the rule.");
return 1;
