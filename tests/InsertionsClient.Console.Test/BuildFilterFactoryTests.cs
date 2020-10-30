// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.ConsoleApp;
using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace InsertionsClient.Console.Test
{
    [TestClass]
    public class BuildFilterFactoryTests
    {
        [TestMethod]
        [DataRow(null, DisplayName = "null-string")]
        [DataRow("", DisplayName = "empty-string")]
        [DataRow(";")]
        [DataRow(";;")]
        [DataRow(",")]
        [DataRow(",;,;")]
        [DataRow("no-equals-sign-here", DisplayName = "missing-equals-sign")]
        [DataRow("repo=", DisplayName = "missing-regex")]
        [DataRow("repo=*(", DisplayName = "invalid-regex")]
        [DataRow("=someregex", DisplayName = "missing-property")]
        [DataRow("invalidprop=someregex", DisplayName = "invalid-property")]
        public void TestInvalidRules(string input)
        {
            bool filterCreated = BuildFilterFactory.TryCreateFromString(input, out Predicate<Build>? buildFilter);
            Assert.IsFalse(filterCreated, "Filter creation should have failed.");
            Assert.IsNull(buildFilter, "Created filter should have been null.");
        }

        [TestMethod]
        // 1 rule, 1 ruleset
        [DataRow("repo=hey", "hey")]
        [DataRow("repo=h.y", "hey")]
        [DataRow("repo=^h.*s$", "here is a test& \\' cas^e with % symbols")]
        [DataRow("repo=.*/installer", "www.github.com/dotnet/installer")]
        // 2 rules, 1 ruleset
        [DataRow("repo=.*/installer,channel=.*", "www.github.com/dotnet/installer", "channelName")]
        [DataRow("repo=[M|m]ilkyway,channel=(ea|ti)ger", "Milkyway", "tiger")]
        [DataRow("channel=(ea|ti)ger,repo=[M|m]ilkyway", "Milkyway", "tiger")]
        [DataRow("repo=^a$,channel=c", "a", "c")]
        // 2 rules, 2 rulesets
        [DataRow("repo=.*/installer;repo=.*/aspnet-core", "www.github.com/dotnet/installer")]
        [DataRow("repo=.*/installer;repo=.*/aspnet-core", "www.github.com/dotnet/aspnet-core")]
        [DataRow("repo=.*/dotnet/.*;repo=.*/aspnet-core", "www.github.com/dotnet/aspnet-core")]
        // 4 rules, 2 rulesets
        [DataRow("repo=.*/installer,channel=release/5.0;repo=.*/aspnet-core,channel=release/3.1", "www.github.com/dotnet/installer", "release/5.0")]
        [DataRow("repo=.*/installer,channel=release/5.0;repo=.*/aspnet-core,channel=release/3.1", "www.github.com/dotnet/aspnet-core", "release/3.1")]
        // All valid property names
        [DataRow("repo=.*,commit=.*,branch=.*,buildNumber=.*,channel=.*", "repo", "channel")]
        public void TestMatchingBuilds(string inputFilter, string buildRepo, string? buildChannel = null)
        {
            bool filterCreated = BuildFilterFactory.TryCreateFromString(inputFilter, out Predicate<Build>? buildFilter);
            Assert.IsTrue(filterCreated, "Filter creation should have succeeded.");
            Assert.IsNotNull(buildFilter!, "Created filter shouldn't have been null.");

            Build build = new Build()
            {
                Repository = buildRepo,
                Channels = buildChannel == null ? null :
                    new List<Channel>()
                    {
                        new Channel()
                        {
                            Id = 0,
                            Name = buildChannel
                        }
                    }
            };

            bool didPassFilter = buildFilter!(build);
            Assert.IsTrue(didPassFilter, "Filter shouldn't have returned false for this build.");
        }

        [TestMethod]
        // 1 rule, 1 ruleset
        [DataRow("repo=hey", "hky")]
        [DataRow("repo=f.ll", "not a full match")]
        [DataRow("repo=full.*$", "not a full match")]
        // 2 rules, 1 ruleset
        [DataRow("repo=.*/installer,channel=.*", "www.github.com/dotnet/aspnet-core", "channelName")]
        [DataRow("repo=dotnet,channel=.*", "www.github.com/dotnet/aspnet-core", "channelName")]
        [DataRow("repo=[M|m]ilkyway,channel=(ea|ti)ger", "Milkyhay", "timer")]
        // 2 rules, 2 rulesets
        [DataRow("repo=.*/installer;repo=.*/aspnet-core", "www.github.com/dotnet/core-setup")]
        [DataRow("repo=.*/dotnet/.*;repo=.*/aspnet-core", "www.github.com/microsoft/msbuild")]
        // 4 rules, 2 rulesets
        [DataRow("repo=.*/installer,channel=release/5.0;repo=.*/aspnet-core,channel=release/3.1", "www.github.com/dotnet/installer", "release/3.1")]
        [DataRow("repo=.*/installer,channel=release/5.0;repo=.*/aspnet-core,channel=release/3.1", "www.github.com/dotnet/aspnet-core", "release/5.0")]
        public void TestExcludedBuilds(string inputFilter, string buildRepo, string? buildChannel = null)
        {
            bool filterCreated = BuildFilterFactory.TryCreateFromString(inputFilter, out Predicate<Build>? buildFilter);
            Assert.IsTrue(filterCreated, "Filter creation should have succeeded.");
            Assert.IsNotNull(buildFilter!, "Created filter shouldn't have been null.");

            Build build = new Build()
            {
                Repository = buildRepo,
                Channels = buildChannel == null ? null :
                    new List<Channel>()
                    {
                        new Channel()
                        {
                            Id = 0,
                            Name = buildChannel
                        }
                    }
            };

            bool didPassFilter = buildFilter!(build);
            Assert.IsFalse(didPassFilter, "Filter shouldn't have returned true for this build.");
        }
    }
}
