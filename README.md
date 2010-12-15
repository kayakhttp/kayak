Kayak is a lightweight C# web server featuring native OWIN support.

Kayak is Copyright (c) 2010 Benjamin van der Veen. Kayak is licensed under the 
MIT License. See LICENSE.txt.

[http://kayakhttp.com]
[http://bvanderveen.com]

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


# Acknowledgements

Kayak includes LitJSON, an excellent JSON library written by Leonardo Boshell 
and graciously dedicated to the public domain.

[http://litjson.sourceforge.net/]

