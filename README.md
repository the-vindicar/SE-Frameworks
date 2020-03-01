# SE-Frameworks
Framework of various code snippets for in-game scripting in Space Engineers.
Created using [Malware's DevKit](https://github.com/malware-dev/MDK-SE).

Main components:
## Helpers
Generic use helper classes used by other components. Include such items as:
* IMyTerminalBlock extensions
* IMyTextSurface extensions, aiming to simplify working with sprites
* PID-related classes
* Coroutine-based state machine implementation

## Piloting
A set of classes useful for creating various scripted autopilots. 
Component includes AutoPilot class that implements desired maneuvers using regular thrusters and gyros.

Also includes a number of strategies describing different manners of flight, such as:
* Straight line flight from A to B.
* Dock to target using one of connection blocks (connector, rotor, landing gear).
* Maintain position and aim at the target.
* Fly forward until an obstacle is detected.

## Scheduling
This component describes a way to run multiple independent jobs at once.
Component includes:
* Scheduler that provides an event-driven programming interface.
* Screen manager that allows user to combine sprite output from multiple tasks and assign it to available screens.
* A number of auxiliary classes.

Component also includes the following job classes:
* Battery charge tracker with an ability to emit events when certain charge levels are reached.
* Power monitor displaying battery charge and estimated time to recharge/discharge.
* Storage block capacity tracker.
* Inventory tracker.
* Inventory monitor - uses inventory tracker to display a list of available resources.
* Stock upkeep - uses inventory tracker to queue production of missing components.
* Production monitor - displays current status of production blocks and estimated completion percentage.
* Simplistic solar tracker.
* Door control script - closes doors after a timeout, allows creating mutually exclusive pairs of doors (only one of the pair can be opened). Useful for making simple airlocks.
* System log - keeps track of messages from other jobs and displays them on a screen.
