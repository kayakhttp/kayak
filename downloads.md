---
title: Get Started
layout: default
---

# Downloads

Kayak is an HTTP server which runs runs OWIN applications.

# Example

To run an OWIN app:

    public static void Main(string[] args)
    {
        var server = new KayakServer();
        var pipe = server.Invoke(new OwinApp());
        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
        pipe.Dispose();
    }

