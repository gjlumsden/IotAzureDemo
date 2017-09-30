#  IoT Solution Architecture with Microsoft Azure Demo

![Build status](https://garym2mcloud.visualstudio.com/_apis/public/build/definitions/03889309-9a7d-4eea-8860-ac71a5d35f38/1/badge)

## The Demo
The important part of this demo is its architecture. But first, a simple problem definition is needed. The following requirements were invented for the purposes of the demo:
* A piece of critical industrial equipment is to be remotely monitored
* Collection and storage of all telemetry is required for future processing
* Stakeholders are to be alerted when telemetry may indicate a problem
* The equipment is to be remotely shut down if certain key telemetry values exceed specific limits.
* The solution must be capable of scaling to thousands of sensors
