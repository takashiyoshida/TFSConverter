This program allows you to export data from a TFS repository in a format
suitable for input to [gource][1].  It's based on [code posted][2] 
originally by another developer.


[1]: http://code.google.com/p/gource/
[2]: http://code.google.com/p/gource/issues/detail?id=16

I'm visualizing large repositories (~225 kloc), so this program allows
you to specify a project root and then one or more trees that you want
to dump.  It culminates by dumping a log for all the trees combined.
This allows me to run gource on pieces of the project as well as the whole
thing.

You may want to modify this program, as I've made a two assumptions about
my TFS setup at work.

* We moved from Starteam to TFS, so I have code that extracts the true 
commit date from the commit log created when we transitioned.  If the
extraction fails, it just uses the TFS commit date.

* I'm using avatars downstream with gource, so I munge user names to 
suit my needs.  Specifically, I nix the domain name, and I have a dictionary 
to canonicalize some names that changed in the Starteam to TFS transition.
As above, if the munging fails, it will just use the name in the TFS commit.
