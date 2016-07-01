# Client-side Reasoner Demo

The UURAGE Client-side Reasoner Demo is a C# application for playing scenarios on the client-side.

## Prerequisites

* Visual Studio 2015
* A scenario XML created by the [Scenario Editor](https://github.com/UURAGE/ScenarioEditor) or according to the [UUDSL](http://uudsl.github.io/scenario/namespace)
* A compatible release of the [Scenario Reasoner](https://github.com/UURAGE/ScenarioReasoner)

## Usage

### Configure App.config

Inside the ClientSideReasonerDemo folder there is an App.config file that contains the settings for your directory structure.

* The XML directory should point to the directory where your scenario XMLs are stored.
* The CGI directory should point to the cgi directory of the directory structure setup in the [Scenario Reasoner](https://github.com/UURAGE/ScenarioReasoner)

### Start the application

Now you can open the solution in Visual Studio and run the application.
