using System;
using AlienSignals.Runtime.Core;
using AlienSignals.Runtime.Core.Interfaces;

namespace AlienSignals.Examples;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("--- Basic Signals, Computeds, and Effects ---");

        // 1. Create two signals to hold strings for a first and last name.
        var firstName = Signals.CreateSignal("John");
        var lastName = Signals.CreateSignal("Smith");

        Console.WriteLine($"Initial values: {firstName.Get()} {lastName.Get()}");

        // 2. Create a computed value that automatically combines them.
        // The getter function will re-run whenever firstName or lastName changes.
        // The `_` ignores the `previousValue` parameter for simplicity.
        var fullName = Signals.CreateComputed<string>(_ => $"{firstName.Get()} {lastName.Get()}");
        // Due to CSharp Compiler Limitations, creating a computed must specify its type. I'm trying to come up with a workaround for this.

        // 3. Create an effect to log the full name to the console.
        // The effect runs immediately upon creation and then again whenever `fullName` changes.
        var loggerEffect = Signals.CreateEffect(() =>
        {
            Console.WriteLine($"EFFECT: Full name is now '{fullName.Get()}'");
        });

        Console.WriteLine("\n--- Triggering Updates ---");

        // 4. Change a signal. This will trigger the computed value to update,
        // which in turn will trigger the effect to run again.
        Console.WriteLine("Changing first name to 'Jane'...");
        firstName.Set("Jane");
        
        // You can also get the latest value from the computed directly.
        Console.WriteLine($"Computed value is now: '{fullName.Get()}'");
        
        
        Console.WriteLine("\n\n--- Batching Updates ---");
        Console.WriteLine("Updating first and last name without batching (causes 2 effect runs):");
        
        // Without batching, each .Set() triggers dependent effects immediately.
        firstName.Set("Peter");
        lastName.Set("Jones");

        Console.WriteLine("\nUpdating first and last name WITH batching (causes 1 effect run):");
        
        // 5. Use batching to group multiple changes.
        // Effects will only run once after the batch is finished.
        Signals.StartBatch();
        firstName.Set("Max");
        lastName.Set("Power");
        Console.WriteLine("... inside batch. Effect has not run yet.");
        Signals.EndBatch(); // All effects depending on the changes run here.


        Console.WriteLine("\n\n--- Effect Scopes and Cleanup ---");

        // 6. Use an effect scope to manage the lifecycle of signals/effects.
        IEffect? counterLogger = null;
        ISignal<int>? counter = null;
        
        Console.WriteLine("Creating a new effect scope...");
        var scope = Signals.CreateEffectScope(() =>
        {
            // Everything created inside this lambda is "owned" by the scope.
            counter = Signals.CreateSignal(0);
            
            var isEven = Signals.CreateComputed<bool>(_ => counter.Get() % 2 == 0);
            
            counterLogger = Signals.CreateEffect(() =>
            {
                Console.WriteLine($"SCOPE EFFECT: Counter is {counter.Get()}, which is {(isEven.Get() ? "Even" : "Odd")}");
            });
        });

        // The effect within the scope works as expected.
        Console.WriteLine("\nUpdating counter inside the scope...");
        counter!.Set(1);
        counter!.Set(2);
        
        // 7. Dispose of the scope.
        // This will automatically find and dispose of all effects created within it.
        Console.WriteLine("\n--- Disposing Scope ---");
        scope.Dispose();
        Console.WriteLine("Scope disposed.");

        // 8. Now, changing the signal has no effect, because the logger has been cleaned up.
        Console.WriteLine("\nUpdating counter after scope disposal (effect should not run)...");
        counter!.Set(3);
        counter!.Set(4);
        Console.WriteLine("... no new logs appeared. Cleanup was successful.");
    }
}