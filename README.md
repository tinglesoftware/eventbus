# Simple Multi-Transport Event Bus for .NET

![GitHub Workflow Status](https://img.shields.io/github/workflow/status/tinglesoftware/eventbus/Build%20and%20Publish?style=flat-square)

This repository contains the code for the `Tingle.EventBus` libraries.

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
|`Tingle.EventBus.Transports.Kafka`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.Kafka.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.Kafka/)|Event bus transport based on the open source [Apache Kafka](https://kafka.apache.org/) platform.|
|`Tingle.EventBus.Transports.RabbitMQ`|[![NuGet](https://img.shields.io/nuget/v/Tingle.EventBus.Transports.RabbitMQ.svg)](https://www.nuget.org/packages/Tingle.EventBus.Transports.RabbitMQ/)|Event bus transport based on the open source [Rabbit MQ](https://www.rabbitmq.com/) broker.|

## Usage

WIP

### Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!

### License

The code is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](./LICENSE) file for more information.
