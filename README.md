# IoT Dundee - IoT Solution Architecture with Microsoft Azure Demo

![Build status](https://garym2mcloud.visualstudio.com/_apis/public/build/definitions/03889309-9a7d-4eea-8860-ac71a5d35f38/1/badge)

Demo code &amp; deployment scripts for the [IoT Dundee #1 Architecting an IoT Solution](https://www.meetup.com/iotScotland/events/237702254/) demo

## Build &amp; Deploy

My build and deployment is hosted in [Visual Studio Team Services](https://www.visualstudio.com/team-services/). I will publish a blog post about the build and release process soon.

## The Demo
The important part of this demo is its architecture. But first, a simple problem definition is needed. The following requirements were invented for the purposes of the demo:
* A piece of critical industrial equipment is to be remotely monitored
* Collection and storage of all telemetry is required for future processing
* Stakeholders are to be alerted when telemetry may indicate a problem
* The equipment is to be remotely shut down if certain key telemetry values exceed specific limits.
* The solution must be capable of scaling to thousands of sensors

This readme will be updated with the architecture diagram soon.

## Azure Resources Used & AWS Alternatives

| **Azure Resource** | **Remarks** | **AWS Alternative** |
|:--------------------------------------------------------------------------------------:|:------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|:----------------------------------------------------------------------:|
| [Azure Functions](https://azure.microsoft.com/en-gb/services/functions/) | Serverless SaaS Event Processing supporting multiple event Triggers. | [AWS Lambda](http://docs.aws.amazon.com/lambda/latest/dg/welcome.html) |
| [Azure IoT Hub](https://azure.microsoft.com/en-gb/services/iot-hub/) | Managed service enabling reliable and secure bidirectional communications between IoT devices and an IoT solution. Capable of connecting millions of devices. Supports multiple modern and secure protocols. | [AWS IoT](https://aws.amazon.com/iot-platform/) |
| [Azure Event Hubs](https://azure.microsoft.com/en-gb/services/event-hubs/) | Telemetry ingestion service that collects capable of hyper-scale supporting millions of events. | [Amazon Kinesis Firehose](https://aws.amazon.com/kinesis/firehose/) |
| [Azure Stream Analytics](https://azure.microsoft.com/en-gb/services/stream-analytics/) | Real-time data stream analytics. | [Amazon Kinesis Analytics](https://aws.amazon.com/kinesis/analytics/) |

[More Azure and AWS resource comparisons](https://docs.microsoft.com/en-us/azure/architecture/aws-professional/services)