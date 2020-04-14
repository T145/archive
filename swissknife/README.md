# swissknife
A simple gem with sneaky commands!
***
If you're tired of having to type out long commands excessively, then meet swissknife!
The whole purpose of the project is to provide a slew of shortened and simple commands to help developers.

## Adding a command
To create a new command, start by opening up the `swissknife.rb` file in `lib`, and create a new definition in the `Util`
class, like this:
``` ruby
# lib/swissknife.rb

# put any helper functions in the SwissKnife module, but not in the Util class

# class Util
def hello
  puts 'Hello World!'
end
```
Next, create a new file without an extension with the name of your command. Having it match the name of the definition
you created earlier is recommended. For my example, it would be `hello`. Finally, all that's needed is to add a shebang,
require for the module, and method initialization, as follows:
``` ruby
# bin/hello

#!/usr/bin/env ruby

require 'swissknife'

SwissKnife::Util.new.hello
```

### [Available commands](https://github.com/T145/swissknife/tree/master/bin)
| Command | Description |
|:-------:|:-----------:|
| archive | Creates a simple zip archive painlessly! |
| camo | (Alias for `hide`) |
| c | Clears the console screen (puts the cursor on the bottom for easy console browsing) |
| clean | Removes some of the junk we all encounter, such as the `.DS_Store` |
| computer | (Alias for `pc`) |
| cpy | Simply cross-platform copy command! |
| delete | (Alias for `r`) |
| erase | (Alias for `r`) |
| ga | Basically just like `git add`, but with the added advantage of having the `-A` option when no files are specified |
| gb | Creates a new Git branch or switches to an exsisting one |
| gc | Either clones a repo or commits some content; having `.git` inside the input determines whether or not to clone |
| gopath | Sets the `GOPATH` variable for Go users |
| gp | The equivalent of `git push` |
| hide | Adds a period to the front of a file's name |
| install | A sleek command that can install Homebrew and/or RVM (installs the latest Ruby version alongside it) |
| move | Moves a file or directory from one place to another! |
| pack | Bundles a Ruby project's dependencies to `vendor/bundle` |
| pc | Simple command for toggling the computer's power |
| r | Deletes a file or directory |
| remove | (Alias for `r`) |
| unarchive | Unzips a zip archive |
| wd | Shows the working directory; pretty straight-forward |
| x | Exits a console |

## Contributing
1. Fork it
2. Add or remove some stuff
3. Commit your changes (`git commit -am "My awesome change!"`)
4. Create a new pull request!
