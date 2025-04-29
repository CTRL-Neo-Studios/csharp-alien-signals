# CSharp-Alien-Signals

A custom port of [Alien Signals](https://github.com/stackblitz/alien-signals) to C#.

## Usage

```csharp
// Create a signal
var count = Reactive.CreateSignal(0);

// Create a computed that depends on the signal
var doubled = Reactive.CreateComputed<int>(prev => count.GetValue() * 2);

// Create an effect that logs changes
var disposeEffect = Reactive.CreateEffect(() => {
    Console.WriteLine($"Count: {count.GetValue()}, Doubled: {doubled.GetValue()}");
});

// Update the signal
count.SetValue(5); // Will trigger the effect

// Batch updates
Reactive.StartBatch();
count.SetValue(10);
count.SetValue(20); // Only one effect run at the end
Reactive.EndBatch();

// Clean up
disposeEffect.Stop();
```

## Installation

### NuGet

Pending

### Unity

1. Open the Unity Package Manager.
2. Click the "+" button and select "Add package from git URL...".
3. Enter the URL of this repository.
4. Click "Add".
5. Wait for Unity to download and import the package.
6. Use the package in your scripts.

