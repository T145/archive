require 'fileutils'
require 'git'
require 'os'

require 'swissknife/zipper'

module SwissKnife
  def install_homebrew
    system 'ruby -e "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install)"'
  end

  def install_rvm
    system 'curl -sSL https://get.rvm.io | bash -s stable --ruby'
  end

  class Util
    def exit
      Process.kill 'HUP', Process.ppid
    end

    def clear_screen
      puts '%c[2J' % 27
    end

    def pack
      if system 'bundle --path vendor/bundle'
        puts "\nDependencies prepared successfully!"
      else
        puts "\nSomething went wrong..."
      end
    end

    def git_add(file)
      git = Git.open Dir.pwd

      if file == nil
        git.add all: true
      else
        git.add file
      end
    end

    def git_branch(branch)
      git = Git.open Dir.pwd
      git.branch branch
    end

    def git_clone_or_commit(message)
      git = Git.open Dir.pwd

      if message.include? '.git'
        name = message.split('/')[4]
        name.slice! '.git'
        Git.clone message, name
        puts "Successfully cloned the \"#{name}\" project!"
      else
        git.commit message
        puts "Created commit: \"#{message}\""
      end
    end

    def git_push(remote, branch)
      git = Git.open Dir.pwd
      git.push remote, branch
      puts "Push to #{branch} was successful!"
    end

    def install(program)
      case program
        when 'homebrew' || 'brew'
          install_homebrew
        when 'rvm'
          if system 'brew doctor'
            install_rvm
          else
            install_homebrew
            install_rvm
          end
        else
          puts "I don't know what #{program} is!"
      end
    end

    def gopath
      if OS.windows?
        system 'SET GOPATH=' + Dir.pwd
        puts 'GOPATH = ' + Dir.pwd
      else
        system 'export GOPATH=' + Dir.pwd
        puts 'GOPATH = ' + Dir.pwd
      end if system 'go env'
    end

    ## Programs for cross-platform effeciency

    # r, remove, delete # Replaces:
    # DEL, ERASE & RMDIR for Windows
    # rm -iv -r for Unix
    def delete(file)
      FileUtils.rm_r file, secure: true
    end

    # (just a helpful utility)
    def clean
      junk = %w(.DS_Store _MACOSX Thumbs.db)
      junk.each { |litter| delete litter }
    end

    # cpy # Replaces:
    # XCOPY & COPY for Windows
    # cp -piv -r for Unix
    def copy(file, destination)
      FileUtils.copy_entry file, destination, preserve: true, remove_destination: true
    end

    # hide, camo # Replaces:
    # ATTRIB +h or ATTRIB -h for Windows
    # mv file .file for Unix
    def hide(file)
      FileUtils.mv file, ".#{file}"
    end

    # The better alternative to the power commands on operating systems
    def computer(option)
      case option
        when option == 'shutdown'
          if OS.windows?
            system 'shutdown -l -s'
          else
            system 'shutdown'
          end
        when option == 'reboot' || option == 'restart'
          system 'shutdown -r'
        else
          puts 'Restart or shutdown, those are your options.'
      end
    end

    def archive(dir, destination)
      Zipper.zip dir, destination, true
    end

    def unarchive(zip, destination)
      if zip.include? '.zip' # for those who drag and drop
        Zipper.unzip zip, destination
      else # for those who do
        Zipper.unzip "#{zip}.zip", destination
      end
    end
  end
end