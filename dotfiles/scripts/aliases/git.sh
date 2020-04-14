alias ga="git add -A"
alias gb="git checkout -b"
alias gc="git add -A && git commit -m"
alias gr="git rm -rf"
alias gs="git status"

alias gffs='git flow feature start'
alias gfff='git flow feature finish'
alias gst='git status'
alias gdiffh='git diff HEAD'
alias gdiff='git diff'
alias gr='git reset --hard HEAD^'
alias gm='git merge'
alias gl='git pull'
alias gp='git push'
alias gd='git diff | mate'
alias gau='git add --update'
#alias gaa='git add --all'
alias gcm='git commit -m'
alias gct='git commit'
alias gc='git commit -v'
alias gca='git commit -v -a'
alias gb='git branch'
alias gba='git branch -a'
alias gco='git checkout'
alias gcob='git checkout -b'
alias gcot='git checkout -t'
alias gcotb='git checkout --track -b'
alias glog='git log'
alias glogp='git log --pretty=format:"%h %s" --graph'
alias gprune='git remote prune origin'

#__git_complete gco _git_checkout
#__git_complete gb _git_branch
#__git_complete gl _git_pull
#__git_complete gp _git_push
#__git_complete gm _git_merge
