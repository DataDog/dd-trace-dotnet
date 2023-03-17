using System;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using Samples.AspNetMvc5.Models;

namespace Samples.AspNetMvc5.Controllers
{
    public class UserController : Controller
    {
        private static readonly Type _scopeType = Type.GetType("Datadog.Trace.Scope, Datadog.Trace");
        private static readonly Type _spanType = Type.GetType("Datadog.Trace.Span, Datadog.Trace");
        private static readonly Type _userDetailsType = Type.GetType("Datadog.Trace.UserDetails, Datadog.Trace");
        private static readonly Type _spanExtensionsType = Type.GetType("Datadog.Trace.SpanExtensions, Datadog.Trace");
        private static MethodInfo _spanProperty = _scopeType.GetProperty("Span", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;

        private static MethodInfo _setUserMethod = _spanExtensionsType.GetMethod("SetUser", BindingFlags.Public | BindingFlags.Static);
        private static ConstructorInfo _userDetailsCtor = _userDetailsType.GetConstructor(new[] { typeof(string) });

        [ValidateInput(false)]
        public ActionResult Index()
        {
            var userId = "user3";

            var userDetails = _userDetailsCtor.Invoke(new object[] {userId});

            //var userDetails = new UserDetails()
            //{
            //    Id = userId,
            //};
            //Tracer.Instance.ActiveScope?.Span.SetUser(userDetails);
            var scope = SampleHelpers.GetActiveScope();
            var span = _spanProperty.Invoke(scope, Array.Empty<object>());
            _setUserMethod.Invoke(span, new object[] {userDetails});


            return View();
        }
    }
}
