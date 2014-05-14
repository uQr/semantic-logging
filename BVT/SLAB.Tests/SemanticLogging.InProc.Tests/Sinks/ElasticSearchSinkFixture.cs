﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestObjects;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.TestScenarios;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Tests.Shared.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.InProc.Tests.Sinks
{
    [TestClass]
    public class ElasticsearchSinkFixture
    {
        private readonly string elasticsearchUri = ConfigurationManager.AppSettings["ElasticsearchUri"];
        private string indexPrefix = null;
        private string type = "testtype";

        [TestInitialize]
        public void Initialize()
        {
            try
            {
                ElasticsearchHelper.DeleteIndex(elasticsearchUri);
            }
            catch (Exception exp)
            {
                Assert.Inconclusive(String.Format("Error occured connecting to ES: Message{0}, StackTrace: {1}", exp.Message, exp.StackTrace));
            }
        }

        [TestMethod]
        public void WhenEventsWithDifferentLevels()
        {
            this.indexPrefix = "wheneventswithdifferentlevels";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    logger.Informational("This is informational");
                    logger.Error("This is an error message");
                    logger.Critical("This is a critical message");
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 3);
            Assert.AreEqual<int>(3, result.Hits.Total);
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (long)h.Source["EventId"] == TestEventSource.InformationalEventId));
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (long)h.Source["EventId"] == TestEventSource.InformationalEventId));
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => (long)h.Source["EventId"] == TestEventSource.CriticalEventId));
        }

        [TestMethod]
        public void WhenLoggingMultipleMessages()
        {
            this.indexPrefix = "whenloggingmultiplemessages";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    for (int n = 0; n < 300; n++)
                    {
                        logger.Informational("logging multiple messages " + n.ToString());
                    }
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 300);
            Assert.AreEqual(300, result.Hits.Total);
        }

        [TestMethod]
        public void WhenNoPayload()
        {
            this.indexPrefix = "whennopayload";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithoutPayloadNorMessage();
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
            Assert.AreEqual<int>(1, result.Hits.Total);
            Assert.AreEqual<long>(TestEventSource.EventWithoutPayloadNorMessageId, (long)result.Hits.Hits.ElementAt(0).Source["EventId"]);
        }

        [TestMethod]
        public void WhenEventHasAllValuesForAttribute()
        {
            this.indexPrefix = "wheneventhasallvaluesforattribute";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.AllParametersWithCustomValues();
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
            Assert.AreEqual<int>(1, result.Hits.Total);
            Assert.AreEqual<long>(10001, (long)result.Hits.Hits.ElementAt(0).Source["EventId"]);
        }

        [TestMethod]
        public void WhenSourceIsEnabledAndDisabled()
        {
            this.indexPrefix = "whensourceisenabledanddisabled";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.Critical("This is a critical message");
                    var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
                    Assert.AreEqual<int>(1, result.Hits.Total);

                    listener.DisableEvents(logger);
                    logger.Critical("This is a critical message");
                });

            var finalResult = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
            Assert.AreEqual<int>(1, finalResult.Hits.Total);
        }

        [TestMethod]
        public void WhenEventHasPayload()
        {
            this.indexPrefix = "wheneventhaspayload";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.EventWithPayload("message", 2);
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
            Assert.AreEqual(1, result.Hits.Total);
            Assert.AreEqual("testInstance", (string)result.Hits.Hits.ElementAt(0).Source["InstanceName"]);
            Assert.AreEqual("message", (string)result.Hits.Hits.ElementAt(0).Source["Payload_payload1"]);
            Assert.AreEqual(2, (long)result.Hits.Hits.ElementAt(0).Source["Payload_payload2"]);
        }

        [TestMethod]
        public void WhenInstanceNameIsNull()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentNullException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch(null, elasticsearchUri, "indexPrefix", "type");
                }
            });

            StringAssert.Contains(ex.Message, "Value cannot be null");
            StringAssert.Contains(ex.Message, "Parameter name: instanceName");
        }

        [TestMethod]
        public void WhenInstanceNameIsEmpty()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch(string.Empty, elasticsearchUri, "indexPrefix", "type");
                }
            });

            StringAssert.Contains(ex.Message, "Argument is empty");
            StringAssert.Contains(ex.Message, "Parameter name: instanceName");
        }

        [TestMethod]
        public void WhenConnectionStringIsNull()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentNullException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch("testinstance", null, "indexPrefix", "type");
                }
            });

            StringAssert.Contains(ex.Message, "Value cannot be null");
            StringAssert.Contains(ex.Message, "Parameter name: connectionString");
        }

        [TestMethod]
        public void WhenConnectionStringIsEmpty()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch("testinstance", string.Empty, "indexPrefix", "type");
                }
            });

            StringAssert.Contains(ex.Message, "Argument is empty");
            StringAssert.Contains(ex.Message, "Parameter name: connectionString");
        }

        [TestMethod]
        public void WhenIndexIsNull()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentNullException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch("testinstance", elasticsearchUri, null, "type");
                }
            });

            StringAssert.Contains(ex.Message, "Value cannot be null");
            StringAssert.Contains(ex.Message, "Parameter name: index");
        }

        [TestMethod]
        public void WhenIndexIsEmpty()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch("testinstance", elasticsearchUri, string.Empty, "type");
                }
            });

            StringAssert.Contains(ex.Message, "Argument is empty");
            StringAssert.Contains(ex.Message, "Parameter name: index");
        }

        [TestMethod]
        public void WhenTypeIsNull()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentNullException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch("testinstance", elasticsearchUri, "indexPrefix", null);
                }
            });

            StringAssert.Contains(ex.Message, "Value cannot be null");
            StringAssert.Contains(ex.Message, "Parameter name: type");
        }

        [TestMethod]
        public void WhenTypeIsEmpty()
        {
            var ex = ExceptionAssertHelper.Throws<ArgumentException>(() =>
            {
                using (var listener = new ObservableEventListener())
                {
                    listener.LogToElasticsearch("testinstance", elasticsearchUri, "indexPrefix", string.Empty);
                }
            });

            StringAssert.Contains(ex.Message, "Argument is empty");
            StringAssert.Contains(ex.Message, "Parameter name: type");
        }

        [TestMethod]
        public void WhenBatchSizeIsExceeded()
        {
            this.indexPrefix = "whenbatchsizeisexceeded";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            QueryResult result = null;
            TestScenario.With2Listeners(
                logger,
                (listener1, listener2) =>
                {
                    listener1.LogToElasticsearch("testInstance1", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(20), bufferingCount: 100);
                    listener2.LogToElasticsearch("testInstance2", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(20), bufferingCount: 100);
                    listener1.EnableEvents(logger, EventLevel.LogAlways);
                    listener2.EnableEvents(logger, EventLevel.LogAlways);

                    // 100 events or more will be flushed by count before the buffering interval elapses
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 120; i++)
                    {
                        var messageNumber = i;
                        logTaskList.Add(Task.Run(() => logger.Critical(messageNumber + "Critical message")));
                    }

                    Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));

                    // Wait less than the buffering interval for the events to be written and assert
                    // Only the first batch of 100 is written for each listener
                    result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 200, maxPollTime: TimeSpan.FromSeconds(10));
                    Assert.AreEqual(200, result.Hits.Total);
                    result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type, "?q=InstanceName:testInstance1");
                    Assert.AreEqual(100, result.Hits.Total);
                    result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type, "?q=InstanceName:testInstance2");
                    Assert.AreEqual(100, result.Hits.Total);
                });

            // The rest of the events are written during the Dispose flush
            result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 240);
            Assert.AreEqual(240, result.Hits.Total);
            result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type, "?q=InstanceName:testInstance1");
            Assert.AreEqual(120, result.Hits.Total);
            result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type, "?q=InstanceName:testInstance2");
            Assert.AreEqual(120, result.Hits.Total);
        }

        [TestMethod]
        public void WhenBufferingWithMinimumNonDefaultInterval()
        {
            this.indexPrefix = "whenbufferingwithminimalnondefaultinterval";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    // Minimum buffering interval is 500 ms
                    var minimumBufferingInterval = TimeSpan.FromMilliseconds(500);
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: minimumBufferingInterval);
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Critical("Critical message");
                    }

                    // Wait for the events to be written and assert
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                    var result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(10, result.Hits.Total);
                });

            // No more events should be written during the Dispose flush
//            var finalResult = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
//            Assert.AreEqual(10, finalResult.Hits.Total);
        }

        [TestMethod]
        public void WhenUsingNonDefaultBufferInterval()
        {
            this.indexPrefix = "whenusingnondefaultbufferinterval";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var bufferingInterval = TimeSpan.FromSeconds(5);
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: bufferingInterval);
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    // Pre-condition: Wait for the events to be written and assert
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                    var result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(0, result.Hits.Total);

                    for (int i = 0; i < 10; i++)
                    {
                        logger.Critical("Critical Message");
                    }

                    // Event must not be written before the interval has elapsed
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                    result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(0, result.Hits.Total);

                    // Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();

                    // 1st interval: Wait for the events to be written and assert
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                    result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(10, result.Hits.Total);
                });
        }

        [TestMethod]
        public void WhenInternalBufferCountIsExceededAndIntervalExceeded()
        {
            this.indexPrefix = "wheninternalbuffercountisexceededandintervalexceeded";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var bufferingInterval = TimeSpan.FromSeconds(5);
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: bufferingInterval, bufferingCount: 100);
                    listener.EnableEvents(logger, EventLevel.Informational);

                    // When reaching 100 events buffer will be flushed
                    for (int i = 0; i < 110; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // Wait for buffer interval to elapse
                    Task.Delay(bufferingInterval).Wait();
                    var result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(100, result.Hits.Total);
                });

            // Last events should be written during the Dispose flush
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            var finalResult = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
            Assert.AreEqual(110, finalResult.Hits.Total);
        }

        [TestMethod]
        public void WhenBufferIntervalExceedsAndLessEntriesThanBufferCount()
        {
            this.indexPrefix = "whenbufferintervalexceedsandlessentriesthanbuffercount";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);

            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    var bufferingInterval = TimeSpan.FromSeconds(2);
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: bufferingInterval);
                    listener.EnableEvents(logger, EventLevel.Informational);

                    // 100 events or more will be flushed by count before the buffering interval elapses
                    for (int i = 0; i < 90; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // Wait for buffer interval to elapse and allow time for events to be written
                    Task.Delay(bufferingInterval.Add(TimeSpan.FromSeconds(3))).Wait();
                    var result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(90, result.Hits.Total);
                });
        }

        [TestMethod]
        public void WhenEventsInThreeConsecutiveIntervals()
        {
            this.indexPrefix = "wheneventsinthreeconsecutiveintervals";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            var bufferingInterval = TimeSpan.FromSeconds(5);
            var insertionInterval = TimeSpan.FromSeconds(3);
            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: bufferingInterval);
                    listener.EnableEvents(logger, EventLevel.Informational);

                    // 1st interval: Log 10 events
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // 1st interval: Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    // 2nd interval: start

                    // 1st interval: Wait for the events to be written and assert
                    Task.Delay(insertionInterval).Wait();
                    var result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(10, result.Hits.Total);

                    // 2nd interval: Log 10 events
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // 2nd interval: Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    // 3rd interval: start

                    // 2nd interval: Wait for the events to be written and assert
                    Task.Delay(insertionInterval).Wait();
                    result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(20, result.Hits.Total);

                    // 3rd interval: Log 10 events
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Informational("Message1");
                    }

                    // 3rd interval: Wait for the buffer to flush at end of interval
                    Task.Delay(bufferingInterval).Wait();
                    // 4th interval: start

                    // 3rd interval: Wait for the events to be written and assert
                    Task.Delay(insertionInterval).Wait();
                    result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
                    Assert.AreEqual(30, result.Hits.Total);

                    // No errors should have been reported
                    Assert.AreEqual(string.Empty, errorsListener.ToString());
                });

            // No more events should have been written during the last flush in the Dispose
            var finalResult = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
            Assert.AreEqual(30, finalResult.Hits.Total);
        }

        [TestMethod]
        public void WhenSourceEnabledWitKeywordsAll()
        {
            this.indexPrefix = "whensourceenabledwitkeywordsall";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.ErrorWithKeywordDiagnostic("Error with keyword Diagnostic");
                    logger.CriticalWithKeywordPage("Critical with keyword Page");
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 2);
            Assert.AreEqual(2, result.Hits.Total);
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => h.Source["Keywords"].ToString() == "1"));
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => h.Source["Keywords"].ToString() == "4"));
        }

        [TestMethod]
        public void WhenNotEnabledWithKeywordsAndEventWithSpecificKeywordIsRaised()
        {
            this.indexPrefix = "whennotenabledwithkeywordsandeventwithspecifickeywordisraised";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.ErrorWithKeywordDiagnostic("Error with keyword EventlogClassic");
                });

            // Wait for events to be inserted
            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            var result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
            Assert.AreEqual(0, result.Hits.Total);
        }

        [TestMethod]
        public void WhenListenerIsDisposed()
        {
            this.indexPrefix = "whenlistenerisdisposed";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With2Listeners(
                logger,
                (listener1, listener2) =>
                {
                    listener1.LogToElasticsearch("testInstance1", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener2.LogToElasticsearch("testInstance2", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener1.EnableEvents(logger, EventLevel.LogAlways);
                    listener2.EnableEvents(logger, EventLevel.LogAlways);
                    var logTaskList = new List<Task>();
                    for (int i = 0; i < 105; i++)
                    {
                        var messageNumber = i;
                        logTaskList.Add(Task.Run(() => logger.Critical(messageNumber + "Critical message")));
                    }

                    Task.WaitAll(logTaskList.ToArray(), TimeSpan.FromSeconds(10));
                    listener1.Dispose();
                    listener2.Dispose();

                    var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 210);
                    Assert.AreEqual(210, result.Hits.Total);
                });
        }

        [TestMethod]
        public void WhenEventWithTaskNameInAttributeIsRaised()
        {
            this.indexPrefix = "wheneventwithtasknameinattributeisraised";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = TestEventSource.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways, Keywords.All);
                    logger.CriticalWithTaskName("Critical with task name");
                    logger.CriticalWithKeywordPage("Critical with no task name");
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 2);
            Assert.AreEqual(2, result.Hits.Total);
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => h.Source["Task"].ToString() == "64513"));
            Assert.IsNotNull(result.Hits.Hits.SingleOrDefault(h => h.Source["Task"].ToString() == "1"));
        }

        [TestMethod]
        public void WhenEventWithEnumsInPayloadIsRaised()
        {
            this.indexPrefix = "wheneventhaspayload";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = MockEventSourceInProcEnum.Logger;

            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    logger.SendEnumsEvent17(MockEventSourceInProcEnum.MyColor.Blue, MockEventSourceInProcEnum.MyFlags.Flag2);
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
            Assert.AreEqual(1, result.Hits.Total);
            Assert.AreEqual(1, (long)result.Hits.Hits.ElementAt(0).Source["Payload_a"]);
            Assert.AreEqual(2, (long)result.Hits.Hits.ElementAt(0).Source["Payload_b"]);
        }

        [TestMethod]
        public void WhenOneSourceTwoListeners()
        {
            this.indexPrefix = "whenonesourcetwolisteners";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = MockEventSource.Logger;

            string errorMessage = string.Concat("Error ", Guid.NewGuid());
            string infoMessage = string.Concat("Message", Guid.NewGuid());
            TestScenario.With2Listeners(
                logger,
                (listener1, listener2) =>
                {
                    listener1.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener2.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener1.EnableEvents(logger, EventLevel.Error);
                    listener2.EnableEvents(logger, EventLevel.Informational);
                    logger.Informational(infoMessage);
                    logger.Error(errorMessage);
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 3);
            Assert.AreEqual(3, result.Hits.Total);
            Assert.AreEqual(2, result.Hits.Hits.Where(h => (long)h.Source["Level"] == (long)EventLevel.Error).Count());
            Assert.AreEqual(1, result.Hits.Hits.Where(h => (long)h.Source["Level"] == (long)EventLevel.Informational).Count());
        }

        [TestMethod]
        public void WhenOneListenerTwoSources()
        {
            this.indexPrefix = "whenonelistenertwosources";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = MockEventSource.Logger;
            var logger2 = MockEventSource2.Logger;

            TestScenario.With1Listener(
                new EventSource[] { logger, logger2 },
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    string message = string.Concat("Message ", Guid.NewGuid());
                    string errorMessage = string.Concat("Error ", Guid.NewGuid());
                    listener.EnableEvents(logger, EventLevel.LogAlways);
                    listener.EnableEvents(logger2, EventLevel.LogAlways);
                    logger.Informational(message);
                    logger2.Error(errorMessage);
                });

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 2);
            Assert.AreEqual(2, result.Hits.Total);
            Assert.AreEqual(1, result.Hits.Hits.Where(h => (long)h.Source["Level"] == (long)EventLevel.Error).Count());
            Assert.AreEqual(1, result.Hits.Hits.Where(h => (long)h.Source["Level"] == (long)EventLevel.Informational).Count());
            Assert.AreNotEqual(Guid.Parse(result.Hits.Hits.ElementAt(0).Source["ProviderId"].ToString()), Guid.Parse(result.Hits.Hits.ElementAt(1).Source["ProviderId"].ToString()));
        }

        [TestMethod]
        public void WhenActivityId()
        {
            this.indexPrefix = "whenactivityid";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = MockEventSource.Logger;

            var activityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            var message = string.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    message = string.Concat("Message ", Guid.NewGuid());
                    logger.Informational(message);
                });

            EventSource.SetCurrentThreadActivityId(previousActivityId);

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
            Assert.AreEqual(1, result.Hits.Total);
            var loggedEvent = result.Hits.Hits.ElementAt(0);
            Assert.AreEqual((int)EventLevel.Informational, (long)loggedEvent.Source["Level"]);
            Assert.AreEqual(1, (long)loggedEvent.Source["EventId"]);
            Assert.AreEqual("testInstance", loggedEvent.Source["InstanceName"].ToString());
            Assert.AreEqual(message, (string)loggedEvent.Source["Payload_message"]);
            Assert.AreEqual(activityId, Guid.Parse(loggedEvent.Source["ActivityId"].ToString()));
            Assert.IsFalse(loggedEvent.Source.ContainsKey("RelatedActivityId"));
        }

        [TestMethod]
        public void WhenActivityIdAndRelatedActivityId()
        {
            this.indexPrefix = "whenactivityidandrelatedactivityid";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = MockEventSource.Logger;

            var activityId = Guid.NewGuid();
            var relatedActivityId = Guid.NewGuid();
            var previousActivityId = Guid.Empty;
            var message = string.Empty;
            EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
            TestScenario.With1Listener(
                logger,
                listener =>
                {
                    listener.LogToElasticsearch("testInstance", elasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    EventSource.SetCurrentThreadActivityId(activityId, out previousActivityId);
                    message = string.Concat("Message ", Guid.NewGuid());
                    logger.InformationalWithRelatedActivityId(message, relatedActivityId);
                });

            EventSource.SetCurrentThreadActivityId(previousActivityId);

            var result = ElasticsearchHelper.PollUntilEvents(this.elasticsearchUri, index, this.type, 1);
            Assert.AreEqual(1, result.Hits.Total);
            var loggedEvent = result.Hits.Hits.ElementAt(0);
            Assert.AreEqual((int)EventLevel.Informational, (long)loggedEvent.Source["Level"]);
            Assert.AreEqual(14, (long)loggedEvent.Source["EventId"]);
            Assert.AreEqual("testInstance", loggedEvent.Source["InstanceName"].ToString());
            Assert.AreEqual(message, (string)loggedEvent.Source["Payload_message"]);
            Assert.AreEqual(activityId, Guid.Parse(loggedEvent.Source["ActivityId"].ToString()));
            Assert.AreEqual(relatedActivityId, Guid.Parse(loggedEvent.Source["RelatedActivityId"].ToString()));
        }

        [TestMethod]
        public void WhenExceptionsAreRoutedToErrorEventSource()
        {
            this.indexPrefix = "whenexceptionsareroutedtoerroreventsource";
            var index = string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy.MM.dd}", this.indexPrefix, DateTime.UtcNow);
            var logger = MockEventSource.Logger;

            var invalidElasticsearchUri = "http://invalid-elastic-search-uri";
            TestScenario.With1Listener(
                logger,
                (listener, errorsListener) =>
                {
                    listener.LogToElasticsearch("testInstance", invalidElasticsearchUri, this.indexPrefix, this.type, bufferingInterval: TimeSpan.FromSeconds(1));
                    listener.EnableEvents(logger, EventLevel.LogAlways);

                    logger.Informational("Message 1");

                    errorsListener.WaitEvents.Wait(TimeSpan.FromSeconds(5));
                    StringAssert.Contains(errorsListener.ToString(), @"An Elasticsearch sink failed to write a batch of events");
                    StringAssert.Contains(errorsListener.ToString(), @"The remote name could not be resolved: 'invalid-elastic-search-uri'");
                });

            var result = ElasticsearchHelper.GetEvents(this.elasticsearchUri, index, this.type);
            Assert.AreEqual(0, result.Hits.Total);
        }
    }
}