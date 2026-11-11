// <copyright file="TestAppCasePlanner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal static class TestAppCasePlanner
    {
        internal enum Kind
        {
            Run,
            Skip,
            Error,
        }

        public static IReadOnlyList<Case> Plan(
            TestApplication application,
            TestFramework[] exact,
            TestFramework minimum,
            TestFramework maximum,
            TestFramework[] excluded,
            IReadOnlyCollection<string> builtFolderNames,
            bool windows,
            bool arm64)
        {
            if (!TestApplicationCatalog.TryGet(application, out var descriptor))
            {
                return new[] { Case.Error($"Unknown test application '{application}'.") };
            }

            if (minimum == TestFramework.Net48 || maximum == TestFramework.Net48)
            {
                return new[]
                {
                    Case.Error("net48 cannot be used as MinimumFramework or MaximumFramework. List TestFramework.Net48 explicitly."),
                };
            }

            var cases = new List<Case>();
            var built = new HashSet<TestFramework>();
            foreach (var folderName in builtFolderNames)
            {
                if (!TestFrameworkInfo.TryParse(folderName, out var framework))
                {
                    cases.Add(Case.Error($"Unknown TFM folder '{folderName}' for application '{descriptor.FolderName}'."));
                    continue;
                }

                built.Add(framework);
            }

            foreach (var framework in Select(exact, minimum, maximum, excluded, windows, arm64))
            {
                if (built.Contains(framework))
                {
                    cases.Add(Case.Run(framework));
                }
                else if (!TestFrameworkPlatform.IsBuilt(framework, windows, arm64))
                {
                    var moniker = TestFrameworkInfo.ToMoniker(framework);
                    cases.Add(Case.Skip(framework, $"Test case skipped: {moniker} is not built for this OS/architecture"));
                }
            }

            return cases;
        }

        private static List<TestFramework> Select(
            TestFramework[] exact,
            TestFramework minimum,
            TestFramework maximum,
            TestFramework[] excluded,
            bool windows,
            bool arm64)
        {
            var hasExact = exact != null && exact.Length > 0;
            var hasRange = minimum != TestFramework.None || maximum != TestFramework.None;

            IEnumerable<TestFramework> selected;
            if (hasExact)
            {
                selected = exact.Where(framework => framework != TestFramework.None);
                if (hasRange)
                {
                    // A range adds versioned frameworks. It never adds net48; an explicit net48 entry stays.
                    var ranged = TestFrameworkInfo.VersionOrder.Where(framework => InRange(framework, minimum, maximum));
                    selected = selected.Where(framework => InRange(framework, minimum, maximum)).Concat(ranged);
                }
            }
            else if (hasRange)
            {
                selected = TestFrameworkInfo.VersionOrder.Where(framework => InRange(framework, minimum, maximum));
            }
            else
            {
                selected = TestFrameworkInfo.VersionOrder.Where(framework => TestFrameworkPlatform.IsBuilt(framework, windows, arm64));
            }

            if (excluded != null && excluded.Length > 0)
            {
                var excludedSet = new HashSet<TestFramework>(excluded);
                selected = selected.Where(framework => !excludedSet.Contains(framework));
            }

            return selected
                .Distinct()
                .OrderBy(framework => framework == TestFramework.Net48 ? int.MinValue : (int)framework)
                .ToList();
        }

        private static bool InRange(TestFramework framework, TestFramework minimum, TestFramework maximum)
        {
            // An explicit net48 entry stays selected when a range is also set.
            // Ranges themselves never add net48.
            if (framework == TestFramework.Net48)
            {
                return true;
            }

            if (framework == TestFramework.None)
            {
                return false;
            }

            if (minimum != TestFramework.None && framework < minimum)
            {
                return false;
            }

            if (maximum != TestFramework.None && framework > maximum)
            {
                return false;
            }

            return true;
        }

        internal sealed class Case
        {
            private Case(Kind kind, TestFramework framework, string message)
            {
                Kind = kind;
                Framework = framework;
                Message = message;
            }

            public Kind Kind { get; }

            public TestFramework Framework { get; }

            public string Message { get; }

            public static Case Run(TestFramework framework)
            {
                return new Case(Kind.Run, framework, null);
            }

            public static Case Skip(TestFramework framework, string message)
            {
                return new Case(Kind.Skip, framework, message);
            }

            public static Case Error(string message)
            {
                return new Case(Kind.Error, TestFramework.None, message);
            }
        }
    }
}
