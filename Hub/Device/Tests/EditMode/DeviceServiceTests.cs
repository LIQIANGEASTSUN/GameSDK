using System;
using System.Collections.Generic;
using System.Reflection;
using GameInterface;
using NUnit.Framework;
using UnityEngine;

namespace GameSDK
{
    public sealed class DeviceServiceTests
    {
        [Test]
        public void EditorFactoryCreatesIndependentFallbackInstances()
        {
            IDevice first = DeviceService.Create();
            IDevice second = DeviceService.Create();
            var ignoredParameters = new Dictionary<string, object> { ["account"] = 42 };

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.GetDeviceId(), Is.EqualTo(SystemInfo.deviceUniqueIdentifier ?? string.Empty));
            Assert.That(first.GetDeviceId(ignoredParameters), Is.EqualTo(first.GetDeviceId()));
            Assert.That(first.GetAdvertisingId(), Is.Empty);
            Assert.That(first.GetAdvertisingTrackingEnabled(), Is.False);
            first.DeleteIosDeviceId();
            first.DeleteIosDeviceId(ignoredParameters);
            Assert.That(first.GetDeviceId(), Is.EqualTo(second.GetDeviceId()));
        }

        [Test]
        public void OptionsRejectNullDictionary()
        {
            TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(() => ParseOptions(null));
            Assert.That(wrapper.InnerException, Is.TypeOf<ArgumentNullException>());
            Assert.That(((ArgumentNullException)wrapper.InnerException).ParamName, Is.EqualTo("parameters"));
        }

        [TestCase("account")]
        [TestCase("service")]
        [TestCase("identifier")]
        public void OptionsRejectMissingRequiredKey(string key)
        {
            Dictionary<string, object> parameters = ValidParameters();
            parameters.Remove(key);
            AssertInvalidParameters(parameters, key);
        }

        [TestCase("account")]
        [TestCase("service")]
        [TestCase("identifier")]
        [TestCase("description")]
        public void OptionsRejectNullAndNonStringForEveryKnownKey(string key)
        {
            Dictionary<string, object> parameters = ValidParameters();
            parameters[key] = null;
            AssertInvalidParameters(parameters, key);
            parameters[key] = 42;
            ArgumentException exception = AssertInvalidParameters(parameters, key);
            Assert.That(exception.Message, Does.Not.Contain("42"));
        }

        [TestCase("account", "")]
        [TestCase("account", " \t")]
        [TestCase("service", "")]
        [TestCase("service", " \t")]
        [TestCase("identifier", "")]
        [TestCase("identifier", " \t")]
        public void OptionsRejectEmptyRequiredIdentity(string key, string value)
        {
            Dictionary<string, object> parameters = ValidParameters();
            parameters[key] = value;
            AssertInvalidParameters(parameters, key);
        }

        [TestCase("account")]
        [TestCase("service")]
        [TestCase("identifier")]
        [TestCase("description")]
        public void OptionsRejectEmbeddedNulForEveryKnownKey(string key)
        {
            Dictionary<string, object> parameters = ValidParameters();
            parameters[key] = "private-value\0suffix";
            ArgumentException exception = AssertInvalidParameters(parameters, key);
            Assert.That(exception.Message, Does.Not.Contain("private-value"));
        }

        [Test]
        public void OptionsAllowMissingEmptyOrWhitespaceDescription()
        {
            Dictionary<string, object> parameters = ValidParameters();
            parameters.Remove("description");
            Assert.That(GetOption(ParseOptions(parameters), "Description"), Is.Empty);
            parameters["description"] = string.Empty;
            Assert.That(GetOption(ParseOptions(parameters), "Description"), Is.Empty);
            parameters["description"] = " \t";
            Assert.That(GetOption(ParseOptions(parameters), "Description"), Is.EqualTo(" \t"));
        }

        [Test]
        public void OptionsPreserveUnicodeWhitespaceAndCaseWithoutSetters()
        {
            var parameters = new Dictionary<string, object>
            {
                ["account"] = " 账号A ",
                ["service"] = " 服务B ",
                ["identifier"] = " 标识C ",
                ["description"] = " 描述D "
            };
            object options = ParseOptions(parameters);
            AssertOptions(options, " 账号A ", " 服务B ", " 标识C ", " 描述D ");
            Assert.That(options.GetType().IsNotPublic, Is.True);
            Assert.That(options.GetType().IsSealed, Is.True);
            Assert.That(options.GetType().GetConstructors(), Is.Empty);
            foreach (var property in options.GetType().GetProperties())
                Assert.That(property.GetSetMethod(true), Is.Null);
        }

        [Test]
        public void OptionsIgnoreUnknownKeysAndTheirValues()
        {
            Dictionary<string, object> parameters = ValidParameters();
            parameters["unknown-null"] = null;
            parameters["unknown-object"] = new object();
            parameters["unknown-string"] = "invalid\0value";
            AssertOptions(ParseOptions(parameters), "account-value", "service-value", "identifier-value", "description-value");
        }

        [Test]
        public void OptionsUseTheSuppliedDictionaryComparer()
        {
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ACCOUNT"] = "account-value",
                ["SERVICE"] = "service-value",
                ["IDENTIFIER"] = "identifier-value",
                ["DESCRIPTION"] = "description-value"
            };
            AssertOptions(ParseOptions(parameters), "account-value", "service-value", "identifier-value", "description-value");
            AssertInvalidParameters(new Dictionary<string, object>(parameters, StringComparer.Ordinal), "account");
        }

        [Test]
        public void OptionsAreIndependentSnapshotsAndEachParseReadsCurrentValues()
        {
            Dictionary<string, object> parameters = ValidParameters();
            object first = ParseOptions(parameters);
            parameters["account"] = "account-next";
            parameters["service"] = "service-next";
            parameters["identifier"] = "identifier-next";
            parameters["description"] = "description-next";
            object next = ParseOptions(parameters);
            object independent = ParseOptions(ValidParameters());

            Assert.That(next, Is.Not.SameAs(first));
            Assert.That(independent, Is.Not.SameAs(first));
            AssertOptions(first, "account-value", "service-value", "identifier-value", "description-value");
            AssertOptions(next, "account-next", "service-next", "identifier-next", "description-next");
            AssertOptions(independent, "account-value", "service-value", "identifier-value", "description-value");
        }

        private static Dictionary<string, object> ValidParameters()
        {
            return new Dictionary<string, object>
            {
                ["account"] = "account-value",
                ["service"] = "service-value",
                ["identifier"] = "identifier-value",
                ["description"] = "description-value"
            };
        }

        private static ArgumentException AssertInvalidParameters(Dictionary<string, object> parameters, string key)
        {
            TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(() => ParseOptions(parameters));
            Assert.That(wrapper.InnerException, Is.TypeOf<ArgumentException>());
            var exception = (ArgumentException)wrapper.InnerException;
            Assert.That(exception.ParamName, Is.EqualTo("parameters"));
            Assert.That(exception.Message, Does.Contain(key));
            return exception;
        }

        private static object ParseOptions(Dictionary<string, object> parameters)
        {
            Type type = typeof(DeviceService).Assembly.GetType("GameSDK.IOSKeychainOptions", true);
            MethodInfo method = type.GetMethod("FromParameters", BindingFlags.Static | BindingFlags.NonPublic);
            return method.Invoke(null, new object[] { parameters });
        }

        private static string GetOption(object options, string property)
        {
            return (string)options.GetType().GetProperty(property).GetValue(options);
        }

        private static void AssertOptions(object options, string account, string service, string identifier, string description)
        {
            Assert.That(GetOption(options, "Account"), Is.EqualTo(account));
            Assert.That(GetOption(options, "Service"), Is.EqualTo(service));
            Assert.That(GetOption(options, "Identifier"), Is.EqualTo(identifier));
            Assert.That(GetOption(options, "Description"), Is.EqualTo(description));
        }
    }
}
