[![NuGet](https://img.shields.io/nuget/v/Easy.MessageHub.svg)](https://www.nuget.org/packages/Easy.MessageHub) [![Build status](https://ci.appveyor.com/api/projects/status/64mfpw9w8lr7dt0j?svg=true)](https://ci.appveyor.com/project/NimaAra/easy-messagehub)

# Easy.MessageHub
An implementation of the Event Aggregator Pattern.

Supports _.Net Core_ (_.Net 4.5_ & _netstandard1.0_) running on:
* .Net Core
* .Net Framework 4.5 and above
* Mono & Xamarin
* UWP
* Windows 8.0
* Windows Phone 8.1
* Windows Phone Seilverlight 8.0

##### If you enjoy what I build then please <a href="https://www.buymeacoffee.com/sP0BhM9n6" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" width="120px" ></a> :-)
___


### Usage example:

Start by creating an instance of the hub:
```csharp
IMessageHub hub = new MessageHub();
```

You can now use the hub to subscribe to any publication of a given type:
```csharp
Guid token = hub.Subscribe<Person>(p => Console.WriteLine($"Id is: {p.Id}"));
// or    
Action<string> action = message => Console.WriteLine($"Message is: {message}");
Guid anotherToken = hub.Subscribe(action);
```
You can then use the token to do:

```csharp
hub.IsSubscribed(token); // returns true
hub.Unsubscribe(token);
hub.IsSubscribed(token); // returns false
```
Or you can clear all subscriptions by:
```csharp
hub.ClearSubscriptions();
```
Publication is as easy as:

```csharp
hub.Publish(new Person { Id = "Foo" });
hub.Publish("An important message");
```

#### Error handling:
The hub catches any exception thrown at the time of publication and exposes them via:
```csharp
hub.RegisterGlobalErrorHandler((token, e) => Console.WriteLine($"Error Publishing, Token: {token} | Exception: {e}"));
```

#### Global handler:
The hub allows the registration of a single handler which will receive every message published by the hub. This can be useful in scenarios where every message published should be logged or audited.

```csharp
hub.RegisterGlobalHandler((type, eventObject) => Console.WriteLine($"Type: {type} - Event: {eventObject}"));
```

#### Event throttling:
The hub allows each subscriber to throttle the rate at which it receives the events:

```csharp
hub.Subscribe<string>(msg => Console.WriteLine($"Message is: {msg}"), TimeSpan.FromSeconds(1));
```
In the above example, if the subscriber receives more than one message within _1_ second of another, it drops them.

#### Inheritance support:
The hub supports inheritance by allowing to subscribe to a base class or an interface and receiving all the publications of types that inherit or implement the subscribed type. For example, given:

```csharp
public class Order {}
public class NewOrder : Order{}
public class BigOrder : Order{}
```

A subscriber registering against `Ordrer` will also receive events of type `NewOrder` and `BigOrder`.
#### More details [HERE](http://www.nimaara.com/2016/02/14/cleaner-pub-sub-using-the-event-aggregator-pattern/)
