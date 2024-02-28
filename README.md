# Simple Multi-Transport Event Bus for .NET

[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.svg)](https://www.nuget.org/packages/Tingle.EventBus/)
![Nuget](https://img.shields.io/nuget/dt/Tingle.EventBus)
[![GitHub Workflow Status](https://github.com/tinglesoftware/eventbus/actions/workflows/build.yml/badge.svg)](https://github.com/tinglesoftware/eventbus/actions/workflows/build.yml)
[![Dependabot](https://badgen.net/badge/Dependabot/enabled/green?icon=dependabot)](https://dependabot.com/)
[![license](https://img.shields.io/github/license/tinglesoftware/eventbus.svg)](LICENSE)

This repository contains the code for the `Tingle.EventBus` libraries. This project exists to simplify the amount of work required to add events to .NET projects. The existing libraries seem to have numerous complexities in setup especially when it comes to the use of framework concepts like dependency injection and options configuration. At [Tingle Software](https://tingle.software), we use this for all our event-driven architecture that is based on .NET

## Packages

|Package|Description|
|--|--|
|[`Tingle.EventBus`](https://www.nuget.org/packages/Tingle.EventBus/)|Base of the event bus library to allow you to publish and consume events from different transports.|
|[`Tingle.EventBus.Serializers.NewtonsoftJson`](https://www.nuget.org/packages/Tingle.EventBus.Serializers.NewtonsoftJson/)|Support for serializing events using [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/).|
|[`Tingle.EventBus.Transports.Amazon.Abstractions`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Amazon.Abstractions/)|Abstractions for working with Amazon (AWS) based transports.|
|[`Tingle.EventBus.Transports.Amazon.Kinesis`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Amazon.Kinesis/)|Transport based on [Amazon Kinesis](https://aws.amazon.com/kinesis/).|
|[`Tingle.EventBus.Transports.Amazon.Sqs`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Amazon.Sqs/)|Transport based on [Amazon Simple Queue Service](https://aws.amazon.com/sqs/).|
|[`Tingle.EventBus.Transports.Azure.Abstractions`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.Abstractions/)|Abstractions for working with Azure based transports.|
|[`Tingle.EventBus.Transports.Azure.EventHubs`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.EventHubs/)|Transport based on [Azure Event Hubs](https://azure.microsoft.com/en-us/services/event-hubs/).|
|[`Tingle.EventBus.Transports.Azure.QueueStorage`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.QueueStorage/)|Transport based on [Azure Storage Queues](https://azure.microsoft.com/en-us/services/storage/queues/).|
|[`Tingle.EventBus.Transports.Azure.ServiceBus`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.ServiceBus/)|Transport based on [Azure Service Bus](https://azure.microsoft.com/en-us/services/service-bus/).|
|[`Tingle.EventBus.Transports.InMemory`](https://www.nuget.org/packages/Tingle.EventBus.Transports.InMemory/)|Transport that works only in memory and in process, useful for testing.|
|[`Tingle.EventBus.Transports.Kafka`](https://www.nuget.org/packages/Tingle.EventBus.Transports.Kafka/)|Transport based on the open source [Apache Kafka](https://kafka.apache.org/) platform.|
|[`Tingle.EventBus.Transports.RabbitMQ`](https://www.nuget.org/packages/Tingle.EventBus.Transports.RabbitMQ/)|Transport based on the open source [RabbitMQ](https://www.rabbitmq.com/) broker.|

## Documentation

A number of the documents below are still a work in progress and will be added as they get ready.

### Getting started

[Bus Concepts and Configuration](./docs/bus-concepts-and-configuration.md)

#### Features

- [Choosing a transport](./docs/transport-selection.md)
- [Multiple transports in one bus](./docs/multi-transport-one-bus.md)
- [Retries](./docs/retries.md)
- [Event and Consumers](./docs/events-and-consumers.md)
- [Observability](./docs/observability.md)
- [Your first app](./docs/your-first-app.md)

#### How to ...

- [Use configuration](./docs/work-with-configuration.md)
- [Work with Azure IoT Hub](./docs/work-with-azure-iot-hub.md)
- [Work with Azure Managed Identities](./docs/work-with-azure-managed-identities.md)
- [Advanced Service Bus options](./docs/advanced-service-bus-options.md)
- [Work with Feature Management](./docs/work-with-feature-management.md)
- [Extend event configuration](./docs/extend-event-configuration.md)

## Samples

- [Using `IConfiguration` to configure the EventBus](./samples/ConfigSample)
- [Simple Consumer](./samples/SimpleConsumer)
- [Simple Publisher](./samples/SimplePublisher)
- [Build a custom event serializer](./samples/CustomEventSerializer)
- [Build a custom event configurator](./samples/CustomEventConfigurator)
- [Consume multiple events in one consumer](./samples/MultiEventsConsumer)
- [Consume same event in multiple consumers](./samples/MultipleConsumers)
- [Publish and consume events from multiple transports of the same type](./samples/MultipleSimilarTransports)
- [Publish and consume events from multiple transports of different types](./samples/MultipleDifferentTransports)
- [In memory background processing](./samples/InMemoryBackgroundProcessing)
- [Using Amazon SQS and SNS](./samples/AmazonSqsAndSns)
- [Receive events from Azure IoT Hub](./samples/AzureIotHub)
- [Using Azure Managed Identity instead of Connection Strings](./samples/AzureManagedIdentity)
- [Health Checks for Azure Service Bus with Managed Identity](./samples/HealthCheck)

## Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!

### License

The Library is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](./LICENSE) file for more information.
