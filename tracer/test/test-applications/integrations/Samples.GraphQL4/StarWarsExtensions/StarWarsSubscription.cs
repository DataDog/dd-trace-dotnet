using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GraphQL;
using GraphQL.Resolvers;
#if !GRAPHQL_5_0 && !GRAPHQL_7_0
using GraphQL.Subscription;
#endif
using GraphQL.Types;
using Human = Samples.GraphQL4.StarWars.Types.Human;
using HumanType = Samples.GraphQL4.StarWars.Types.HumanType;
using StarWarsData = Samples.GraphQL4.StarWars.StarWarsData;

namespace Samples.GraphQL4.StarWarsExtensions
{
    /// <example>
    /// This is an example JSON request for a subscription
    /// {
    ///   "query": "subscription HumanAddedSub{ humanAdded { name } }",
    /// }
    /// </example>
    public class StarWarsSubscription : ObjectGraphType<object>
    {
        private readonly StarWarsData _starWarsData;

        private readonly ISubject<Human> _humanStream = new ReplaySubject<Human>(1);

        public StarWarsSubscription(StarWarsData data)
        {
            Name = "Subscription";
            _starWarsData = data;

            AddField(
#if GRAPHQL_5_0 || GRAPHQL_7_0
                new FieldType
#else
                new EventStreamFieldType
#endif
            {
                Name = "humanAdded",
                Type = typeof(HumanType),
                Resolver = new FuncFieldResolver<Human>(ResolveMessage),
#if GRAPHQL_5_0 || GRAPHQL_7_0
                StreamResolver = new SourceStreamResolver<Human>(Subscribe)
#else
                Subscriber = new EventStreamResolver<Human>(Subscribe)
#endif
            });
            AddField(
#if GRAPHQL_5_0 || GRAPHQL_7_0
                new FieldType
#else
                new EventStreamFieldType
#endif
            {
                Name = "throwNotImplementedException",
                Type = typeof(HumanType),
                Resolver = new FuncFieldResolver<Human>(ResolveMessage),
#if GRAPHQL_5_0 || GRAPHQL_7_0
                StreamResolver = new SourceStreamResolver<Human>(ThrowNotImplementedException)
#else
                Subscriber = new EventStreamResolver<Human>(ThrowNotImplementedException)
#endif
            });
        }

        private Human ResolveMessage(IResolveFieldContext context)
        {
            return context.Source as Human;
        }

        private IObservable<Human> Subscribe(
#if GRAPHQL_5_0 || GRAPHQL_7_0
            IResolveFieldContext context
#else
            IResolveEventStreamContext context
#endif
        )
        {
            List<Human> listOfHumans = new List<Human>();

            var task = _starWarsData.GetHumanByIdAsync("1");
            task.Wait();
            var result = task.Result;
            if (result != null)
            {
                listOfHumans.Add(task.Result);
            }

            task = _starWarsData.GetHumanByIdAsync("2");
            task.Wait();
            result = task.Result;
            if (result != null)
            {
                listOfHumans.Add(task.Result);
            }

            return listOfHumans.ToObservable();
        }

        private IObservable<Human> ThrowNotImplementedException(
#if GRAPHQL_5_0 || GRAPHQL_7_0
            IResolveFieldContext context
#else
            IResolveEventStreamContext context
#endif
        )
        {
            try
            {
                throw new NotImplementedException("This API purposely throws a NotImplementedException");
            }
            catch (Exception ex)
            {
                var error = new ExecutionError("This API purposely throws a NotImplementedException", ex);
#if GRAPHQL_7_0
                error.Extensions = new Dictionary<string, object>
                {
                    { "int", 1 },
                    { "float", 1.1f },
                    { "str", "1" },
                    { "bool", true },
                    { "other", new object[] { 1, "foo" } },
                    { "sbyte", (sbyte)-42 },
                    { "byte", (byte)42 },
                    { "short", (short)-1000 },
                    { "ushort", (ushort)1000 },
                    { "uint", (uint)4294967295 },
                    { "long", (long)-9223372036854775808 },
                    { "ulong", (ulong)18446744073709551615 },
                    { "decimal", (decimal)3.1415926535897932384626433833 },
                    { "double", 3.1415926535897932384626433833 },
                    { "char", 'A' },
                    { "not_captured", "This should not be captured" }
                };
#endif
                throw error;
            }
        }
    }
}
