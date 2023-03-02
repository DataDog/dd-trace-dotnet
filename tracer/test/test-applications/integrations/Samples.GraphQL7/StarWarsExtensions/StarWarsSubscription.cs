using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using Human = Samples.GraphQL7.StarWars.Types.Human;
using HumanType = Samples.GraphQL7.StarWars.Types.HumanType;
using StarWarsData = Samples.GraphQL7.StarWars.StarWarsData;

namespace Samples.GraphQL7.StarWarsExtensions
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

            AddField(new FieldType
            {
                Name = "humanAdded",
                Type = typeof(HumanType),
                Resolver = new FuncFieldResolver<Human>(ResolveMessage),
                StreamResolver = new SourceStreamResolver<Human>(Subscribe)
            });
            AddField(new FieldType
            {
                Name = "throwNotImplementedException",
                Type = typeof(HumanType),
                Resolver = new FuncFieldResolver<Human>(ResolveMessage),
                StreamResolver = new SourceStreamResolver<Human>(ThrowNotImplementedException)
            });
        }

        private Human ResolveMessage(IResolveFieldContext context)
        {
            return context.Source as Human;
        }

        private IObservable<Human> Subscribe(IResolveFieldContext context)
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

        private IObservable<Human> ThrowNotImplementedException(IResolveFieldContext context)
        {
            throw new NotImplementedException("This API purposely throws a NotImplementedException");
        }
    }
}

