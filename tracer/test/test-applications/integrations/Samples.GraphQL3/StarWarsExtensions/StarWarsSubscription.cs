using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Subscription;
using GraphQL.Types;
using Human = Samples.GraphQL3.StarWars.Types.Human;
using HumanType = Samples.GraphQL3.StarWars.Types.HumanType;
using StarWarsData = Samples.GraphQL3.StarWars.StarWarsData;

namespace Samples.GraphQL3.StarWarsExtensions
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

            AddField(new EventStreamFieldType
            {
                Name = "humanAdded",
                Type = typeof(HumanType),
                Resolver = new FuncFieldResolver<Human>(ResolveMessage),
                Subscriber = new EventStreamResolver<Human>(Subscribe)
            });
            AddField(new EventStreamFieldType
            {
                Name = "throwNotImplementedException",
                Type = typeof(HumanType),
                Resolver = new FuncFieldResolver<Human>(ResolveMessage),
                Subscriber = new EventStreamResolver<Human>(ThrowNotImplementedException)
            });
        }

        private Human ResolveMessage(IResolveFieldContext context)
        {
            return context.Source as Human;
        }

        private IObservable<Human> Subscribe(IResolveEventStreamContext context)
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

        private IObservable<Human> ThrowNotImplementedException(IResolveEventStreamContext context)
        {
            throw new NotImplementedException("This API purposely throws a NotImplementedException");
        }
    }
}
