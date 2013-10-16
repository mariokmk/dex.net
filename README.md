dex.net
=======

A Mono/.NET library to parse Android DEX files. Its main purpose is to support utilities for disassembling and presenting the contents of DEX files.

There are multiple front-ends available. A command line tool, a native OSX application and a GTK ui for Linux. All the front-end projects are also available in GitHub.

Dex Display Languages
---------------------

dex.net can display the output of a DEX file in multiple ways. It currently supports 2 languages (or writers in the source):

**Plain Dex**

This format follows the syntax in the [Dalvik bytecode](http://source.android.com/devices/tech/dalvik/dalvik-bytecode.html). The only exceptions are the switch and fill-array opcodes which use data tables. The data tables are parsed and displayed as part of the opcode.

**Dex**

This format provides a direct translation of the 'plain dex' opcodes into a more readable format. It also resolves references to strings, methods, classes, fields. It maintains a 1-1 mapping with standard opcodes.

An example of how this language would format an opcode: 'aput v0, v1, v2' is displayed as 'v1[v2] = v0'


License
-------

Dex.NET is provided under the [Apache 2.0 License](http://www.apache.org/licenses/LICENSE-2.0)
