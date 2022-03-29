using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public struct ActivityStub : IDisposable
    {
#region Private implementation code

        private static class NoOpSingeltons
        {
            internal static readonly ActivityStub ActivityStub = new ActivityStub(null);
            internal static readonly IEnumerable<KeyValuePair<string, string>> KvpEnumerable = new KeyValuePair<string, string>[0];
            internal static readonly TimeSpan TimeSpan = TimeSpan.Zero;
            internal static readonly DateTime DateTimeUtc = default(DateTime).ToUniversalTime();
            internal static readonly ActivityIdFormatStub ActivityIdFormat = ActivityIdFormatStub.Unknown;
            internal static readonly ActivityKindStub ActivityKind = ActivityKindStub.Internal;
        }

        private static readonly ConditionalWeakTable<object, SupplementalActivityData> s_supplementalActivityData = new ConditionalWeakTable<object, SupplementalActivityData>();

        public void GetLocalTraceInfo(out bool isLocalRootActivity, out ulong rootActivitySpanIdHash)
        {
            throw new NotImplementedException();
        }

        private static string FormatNotSupportedErrorMessage(string apiName, string minRequiredFeatureSet, DynamicInvokerOld invoker)
        {
            string errMsg = $"{nameof(ActivityStub)}.{apiName} is not supported."
                                  + $" Status: {{{nameof(DynamicLoader)}.{nameof(DynamicLoader.InitializationState)}={DynamicLoader.InitializationState.ToString()};"
                                  + $" MinRequiredFeatureSet={minRequiredFeatureSet};"
                                  + $" SupportedFeatureSets={invoker.SupportedFeatures.FormatFeatureSetSupportList()}}}";
            return errMsg;
        }

        //public ulong GetSpanIdHash()
        //{
        //    throw new NotImplementedException();
        //}

        private readonly object _activityInstance;

        private ActivityStub(object activityInstance)
        {
            if (activityInstance == null)
            {
                _activityInstance = null;
            }
            else
            {
                DynamicLoader.Invoker.Activity.ValidateType(activityInstance);
                _activityInstance = activityInstance;
            }
        }

        private bool TryGetSupplementalData(out SupplementalActivityData supplementalData)
        {
            if (_activityInstance == null)
            {
                supplementalData = null;
                return false;
            }

            ConditionalWeakTable<object, SupplementalActivityData> supplementalActivityData = s_supplementalActivityData;
            return supplementalActivityData.TryGetValue(_activityInstance, out supplementalData);
        }

        private SupplementalActivityData GetOrCreateSupplementalData()
        {
            if (_activityInstance == null)
            {
                return null;
            }

            ConditionalWeakTable<object, SupplementalActivityData> supplementalActivityData = s_supplementalActivityData;
            return supplementalActivityData.GetValue(_activityInstance, (_) => new SupplementalActivityData());
        }

#endregion Private implementation code

#region Stub-specific public API

        public object ActivityInstance { get { return _activityInstance; } }

        public bool IsNoOpStub { get { return _activityInstance == null; } }

        public static ActivityStub Wrap(object activity)
        {
            if (activity == null)
            {
                return NoOpSingeltons.ActivityStub;
            }
            else
            {
                return new ActivityStub(activity);
            }
        }

#endregion Stub-specific public API

#region Public API stubs for static Activity APIs

        public static ActivityStub Current
        {
            get
            {
                if (!DynamicLoader.EnsureInitialized())
                {
                    return NoOpSingeltons.ActivityStub;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    object currentActivityInstance = invoker.Activity.get_Current();
                    ActivityStub stub = ActivityStub.Wrap(currentActivityInstance);
                    return stub;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage($"{nameof(Current)}", "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public static ActivityIdFormatStub DefaultIdFormat
        {
            get
            {
                if (!DynamicLoader.EnsureInitialized())
                {
                    return ActivityIdFormatStub.Unknown;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000)
                {
                    ActivityIdFormatStub defaultIdFormat = invoker.Activity.get_DefaultIdFormat();
                    return defaultIdFormat;
                }
                else if (invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return ActivityIdFormatStub.Hierarchical;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(DefaultIdFormat), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        // StartNewActivity will expose scenarios that can be supported regardless of the underlying DS version.
        // Overloads available in .NET 5 as of 2020-Sep:
        //public Activity? StartActivity(string name,
        //                               ActivityKind kind = ActivityKind.Internal);
        //
        //public Activity? StartActivity(string name,
        //                               ActivityKind kind,
        //                               ActivityContext parentContext,
        //                               IEnumerable<KeyValuePair<string, object?>>? tags = null,
        //                               IEnumerable<System.Diagnostics.ActivityLink>? links = null,
        //                               System.DateTimeOffset startTime = default);
        //
        //public Activity? StartActivity(string name,
        //                               ActivityKind kind,
        //                               string parentId,
        //                               IEnumerable<KeyValuePair<string, object?>>? tags = null,
        //                               IEnumerable<ActivityLink>? links = null,
        //                               DateTimeOffset startTime = default);
        public static ActivityStub StartNewActivity(string operationName)
        {
            return StartNewActivity(operationName, ActivityKindStub.Internal, tags: null);
        }

        public static ActivityStub StartNewActivity(string operationName, ActivityKindStub activityKind)
        {
            return StartNewActivity(operationName, activityKind, tags: null);
        }

        public static ActivityStub StartNewActivity(string operationName, ActivityKindStub activityKind, IEnumerable<KeyValuePair<string, string>> tags)
        {
            return StartNewActivity(operationName, ActivityKindStub.Internal, parentContext: default(ActivityContextStub), tags);
        }

        public static ActivityStub StartNewActivity(string operationName, ActivityKindStub activityKind, ActivityContextStub parentContext)
        {
            return StartNewActivity(operationName, ActivityKindStub.Internal, parentContext, tags: null);
        }

        public static ActivityStub StartNewActivity(string operationName,
                                                    ActivityKindStub activityKind,
                                                    ActivityContextStub parentContext,
                                                    IEnumerable<KeyValuePair<string, string>> tags)
        {
            if (!DynamicLoader.EnsureInitialized())
            {
                return NoOpSingeltons.ActivityStub;
            }

            DynamicInvokerOld invoker = DynamicLoader.Invoker;
            if (invoker.SupportedFeatures.FeatureSet_5000)
            {
                object activityInstance = invoker.ActivitySource.StartActivity(operationName, activityKind, parentContext, tags);
                ActivityStub activityStub = Wrap(activityInstance);

                return activityStub;
            }
            else if (invoker.SupportedFeatures.FeatureSet_4020)
            {
                object activityInstance = invoker.Activity.Ctor(operationName);
                ActivityStub activityStub = Wrap(activityInstance);

                // Older Activity versions do not have a concept of Trace and Span. Instead they have an Id and a RoodId.
                // We will generate a parent Id to match the logic employed by older activities so that RoodId acts as a TraceId.
                // See:
                // https://github.com/dotnet/runtime/blob/7666224cfcfb4349da28f9f7fb1de931528ef208/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Activity.cs#L39
                // https://github.com/dotnet/runtime/blob/7666224cfcfb4349da28f9f7fb1de931528ef208/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Activity.cs#L358
                // https://github.com/dotnet/runtime/blob/7666224cfcfb4349da28f9f7fb1de931528ef208/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Activity.cs#L405
                // https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md#id-format

                if (parentContext.IsNotInitialized())
                {
                    string parentId = "|" + parentContext.TraceIdHexString + "." + parentContext.SpanIdHexString + "_";
                    invoker.Activity.SetParentId(activityInstance, parentId);
                }

                // Internal is the default. For internal, we avoid creating supplemantal data.
                if (activityKind != ActivityKindStub.Internal)
                {
                    SupplementalActivityData supplementalData = activityStub.GetOrCreateSupplementalData();
                    supplementalData.ActivityKind = activityKind;
                }

                if (tags != null)
                {
                    foreach (KeyValuePair<string, string> tag in tags)
                    {
                        invoker.Activity.AddTag(activityInstance, tag.Key, tag.Value);
                    }
                }

                object diagnosticSource = invoker.DiagnosticListener.DefaultDiagnosticSource;
                invoker.DiagnosticListener.StartActivity(activityInstance, activityInstance);

                return activityStub;
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage($"{nameof(StartNewActivity)}(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public static bool ForceDefaultIdFormat
        {
            get
            {
                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000)
                {
                    return true;
                }
                else if (invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return true; // We only accept hierarchical
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Tags), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }

            set
            {
                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000)
                {
                    //...
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Tags), "5000", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        #endregion Public API stubs for static Activity APIs

        #region Public API stubs for instance Activity APIs

        public string TraceId { get { throw new NotImplementedException(); } } 
        public string SpanId { get { throw new NotImplementedException(); } }
        public string ParentSpanId { get { throw new NotImplementedException(); } }

        public void AddBaggage(string key, string value)
        {
            if (_activityInstance == null)
            {
                return;
            }

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
            {
                invoker.Activity.AddBaggage(_activityInstance, key, value);
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage($"{nameof(AddBaggage)}(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public string GetBaggageItem(string key)
        {
            if (_activityInstance == null)
            {
                return null;
            }

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
            {
                return invoker.Activity.GetBaggageItem(_activityInstance, key);
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage($"{nameof(GetBaggageItem)}(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public void AddTag(string key, string value)
        {
            if (_activityInstance == null)
            {
                return;
            }

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
            {
                invoker.Activity.AddTag(_activityInstance, key, value);
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage($"{nameof(AddTag)}(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public IEnumerable<KeyValuePair<string, string>> Baggage
        {
            get
            {
                if (_activityInstance == null)
                {
                    return NoOpSingeltons.KvpEnumerable;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return invoker.Activity.get_Baggage(_activityInstance);
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Baggage), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public void Dispose()
        {
            if (_activityInstance != null)
            {
                IDisposable disposableActivity = _activityInstance as IDisposable;
                if (disposableActivity != null)
                {
                    disposableActivity.Dispose();
                }
                else
                {
                    Stop();
                }
            }
        }

        public TimeSpan Duration
        {
            get
            {
                if (_activityInstance == null)
                {
                    return NoOpSingeltons.TimeSpan;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return default;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Duration), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public object GetCustomProperty(string propertyName)
        {
            if (_activityInstance == null)
            {
                return null;
            }

            Validate.NotNull(propertyName, nameof(propertyName));

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000)
            {
                return null; //...
            }
            else if (invoker.SupportedFeatures.FeatureSet_4020)
            {
                if (!TryGetSupplementalData(out SupplementalActivityData supplementalData))
                {
                    return null;
                }

                return supplementalData.GetCustomProperty(propertyName);
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage(nameof(GetCustomProperty) + "(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public string Id
        {
            get
            {
                if (_activityInstance == null)
                {
                    return String.Empty;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return default;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Id), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public ActivityIdFormatStub IdFormat
        {
            get
            {
                if (_activityInstance == null)
                {
                    return NoOpSingeltons.ActivityIdFormat;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000)
                {
                    return default(ActivityIdFormatStub);
                }
                else if (invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return ActivityIdFormatStub.Hierarchical;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Tags), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }


        public ActivityKindStub Kind
        {
            get
            {
                if (_activityInstance == null)
                {
                    return NoOpSingeltons.ActivityKind;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000)
                {
                    return default(ActivityKindStub);
                }
                else if (invoker.SupportedFeatures.FeatureSet_4020)
                {
                    if (!TryGetSupplementalData(out SupplementalActivityData supplementalData))
                    {
                        return ActivityKindStub.Internal;
                    }

                    return supplementalData.ActivityKind;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Tags), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public string OperationName
        {
            get
            {
                if (_activityInstance == null)
                {
                    return String.Empty;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return default;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(OperationName), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public bool TryGetParent(out ActivityStub parent)
        {
            if (_activityInstance == null)
            {
                parent = NoOpSingeltons.ActivityStub;
                return false;
            }

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
            {
                object parentObject = default;
                if (parentObject == null)
                {
                    parent = NoOpSingeltons.ActivityStub;
                    return false;
                }
                else
                {
                    parent = ActivityStub.Wrap(parentObject);
                    return true;
                }
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage(nameof(RootId), "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public string RootId
        {
            get
            {
                if (_activityInstance == null)
                {
                    return null;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return default;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(RootId), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public void SetCustomProperty(string propertyName, object propertyValue)
        {
            if (_activityInstance == null)
            {
                return;
            }

            Validate.NotNull(propertyName, nameof(propertyName));

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000)
            {
                //...
            }
            else if (invoker.SupportedFeatures.FeatureSet_4020)
            {
                SupplementalActivityData supplementalData;

                if (propertyValue == null)
                {
                    if (TryGetSupplementalData(out supplementalData))
                    {
                        supplementalData.SetCustomProperty(propertyName, propertyValue);
                    }
                }
                else
                {
                    supplementalData = GetOrCreateSupplementalData();
                    supplementalData.SetCustomProperty(propertyName, propertyValue);
                }
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage(nameof(SetCustomProperty) + "(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }


        public void SetParentId(string parentId)
        {
            if (_activityInstance == null)
            {
                return;
            }

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
            {
                // ...
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage($"{nameof(SetParentId)}(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public DateTime StartTimeUtc
        {
            get
            {
                if (_activityInstance == null)
                {
                    return NoOpSingeltons.DateTimeUtc;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return default;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(StartTimeUtc), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public void Stop()
        {
            if (_activityInstance == null)
            {
                return;
            }

            DynamicInvokerOld invoker = DynamicLoader.Invoker;

            if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
            {
                // ...
            }
            else
            {
                string errMsg = FormatNotSupportedErrorMessage($"{nameof(Stop)}(..)", "4020", invoker);
                throw new NotSupportedException(errMsg);
            }
        }

        public IEnumerable<KeyValuePair<string, string>> Tags
        {
            get
            {
                if (_activityInstance == null)
                {
                    return NoOpSingeltons.KvpEnumerable;
                }

                DynamicInvokerOld invoker = DynamicLoader.Invoker;

                if (invoker.SupportedFeatures.FeatureSet_5000 || invoker.SupportedFeatures.FeatureSet_4020)
                {
                    return default;
                }
                else
                {
                    string errMsg = FormatNotSupportedErrorMessage(nameof(Tags), "4020", invoker);
                    throw new NotSupportedException(errMsg);
                }
            }
        }

        public ActivityStub LocalRoot 
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        #endregion Public API stubs for instance Activity APIs

    }
}
