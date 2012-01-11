# About Kayak

Kayak is an event-driven networking library for .NET. It allows you to easily create TCP clients and servers. Kayak contains an HTTP/1.1 server implementation.

Kayak is Copyright (c) 2007-2011 Benjamin van der Veen. Kayak is licensed under the 
MIT License. See LICENSE.txt.

[http://kayakhttp.com](http://kayakhttp.com)<br>
[http://bvanderveen.com](http://bvanderveen.com)<br>
[Kayak Mailing List](http://groups.google.com/group/kayak-http)

# How to build

Kayak uses Rake, the Ruby build tool, to perform builds. Ruby must be installed on your system to build Kayak. The build script is known to work with Ruby 1.9.2, and depends on [Albacore](https://github.com/derickbailey/Albacore), a suite of Rake tasks for .NET.
Please make sure your github username and token are set before building or the submodules will fail to update. See http://help.github.com/set-your-user-name-email-and-github-token/ for more information

To build:

    $ gem install albacore
    $ git clone git@github.com/kayak/kayak.git
    $ cd kayak
    $ rake

**Note**: You can build Kayak from within your IDE, but you should run the build script first to bootstrap the source tree. The bootstrap process inits and updates some git submodules and downloads some binary dependencies from NuGet.org.

Submodules: 

 - [HttpMachine](http://github.com/bvanderveen/httpmachine)

Binary Dependencies:

 - [NUnit](http://nuget.org/List/Packages/NUnit)

# Bugs

Please use the [GitHub issue tracker](https://github.com/kayak/kayak/issues/new) to report bugs. Use the "Bug" label to tag your issue.

# Discussion

Join the [Kayak Google Group](http://groups.google.com/group/kayak-http) to ask questions, suggest features, collaborate on development, etc.