# Simple Multi-Transport Event Bus for .NET

![Nuget](https://img.shields.io/nuget/dt/Tingle.EventBus)
![GitHub Workflow Status](https://img.shields.io/github/workflow/status/tinglesoftware/eventbus/Build%20and%20Publish?style=flat-square)
[![Dependabot](https://badgen.net/badge/Dependabot/enabled/green?icon=dependabot)](https://dependabot.com/)

This repository contains the code for the `Tingle.EventBus` libraries. This project exists so as to simplify the amount of work required to add events to .NET projects. The existing libraries seem to have numerous complexities in setup especially when it comes to the use of framework concepts like dependency inject and options configuration. At [Tingle Software](https://tingle.software), we use this for all our event-driven architecture that is based on .NET

## Packages

|Package|Version|Description|
|--|--|--|
|`Tingle.EventBus`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.svg)](https://www.nuget.org/packages/Tingle.EventBus/)|Base of the event bus library to allow you publish and consume events from different transports.|
|`Tingle.EventBus.Serializers.NewtonsoftJson`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Serializers.NewtonsoftJson.svg)](https://www.nuget.org/packages/Tingle.EventBus.Serializers.NewtonsoftJson/)|Support for serializing events using [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/).|
|`Tingle.EventBus.Transports.Amazon.Abstractions`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Amazon.Abstractions.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Amazon.Abstractions/)|Abstractions for working with Amazon (AWS) based transports.|
|`Tingle.EventBus.Transports.Amazon.Kinesis`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Amazon.Kinesis.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Amazon.Kinesis/)|Event bus transport based on [Amazon Kinesis](https://aws.amazon.com/kinesis/).|
|`Tingle.EventBus.Transports.Amazon.Sqs`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Amazon.Sqs.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Amazon.Sqs/)|Event bus transport based on [Amazon Simple Queue Service](https://aws.amazon.com/sqs/).|
|`Tingle.EventBus.Transports.Azure.Abstractions`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Azure.Abstractions.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.Abstractions/)|Abstractions for working with Azure based transports.|
|`Tingle.EventBus.Transports.Azure.EventHubs`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Azure.EventHubs.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.EventHubs/)|Event bus transport based on [Azure Event Hubs](https://azure.microsoft.com/en-us/services/event-hubs/).|
|`Tingle.EventBus.Transports.Azure.QueueStorage`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Azure.QueueStorage.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.QueueStorage/)|Event bus transport based on [Azure Storage Queues](https://azure.microsoft.com/en-us/services/storage/queues/).|
|`Tingle.EventBus.Transports.Azure.ServiceBus`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Azure.ServiceBus.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Azure.ServiceBus/)|Event bus transport based on [Azure Service Bus](https://azure.microsoft.com/en-us/services/service-bus/).|
|`Tingle.EventBus.Transports.InMemory`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.InMemory.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.InMemory/)|Event bus transport that works only in memory and in process, useful for testing.|
|`Tingle.EventBus.Transports.Kafka`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Kafka.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Kafka/)|Event bus transport based on the open source [Apache Kafka](https://kafka.apache.org/) platform.|
|`Tingle.EventBus.Transports.RabbitMQ`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.RabbitMQ.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.RabbitMQ/)|Event bus transport based on the open source [Rabbit MQ](https://www.rabbitmq.com/) broker.|

## Documentation

A number fo the documents below are still a work in progress and would be added as they get ready.

### Getting started

[Bus Concepts and Configuration](docs/Bus-Concepts-and-Configuration.md)

#### Features

* [Choosing a transport](docs/Transport-Selection.md)
* [Multiple transports in one bus](docs/Multi-Transport-One-Bus.md)
* [Retries](docs/Retries.md)
* [Event and Consumers](docs/Events-and-Consumers.md)
* [Observability](docs/Observability.md)
* [Your first app](docs/Your-first-app.md)

#### How to ...

* [Work with IoTHub](docs/Work-with-IoTHub.md)
* [Advanced Service Bus options](docs/Advanced-Service-Bus-options.md)
* [Work with Feature Management](docs/Work-with-Feature-Management.md)
* [Extend event configuration](docs/Extend-Event-Configuration.md)

## Samples

* [Simple Consumer](./samples/SimpleConsumer)
* [Simple Publisher](./samples/SimplePublisher)
* [Build a custom event serializer](./samples/CustomEventSerializer)
* [Build a custom event configurator](./samples/CustomEventConfigurator)
* [Consume multiple events in one consumer](./samples/MultiEventsConsumer)
* [Consume same event in multiple consumers](./samples/MultipleConsumers)
* [In memory background processing](./samples/InMemoryBackgroundProcessing)

## Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!

## License

The code is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](./LICENSE) file for more information.
