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
            throw new NotImplementedException("This API purposely throws a NotImplementedException");
        }
    }
}
