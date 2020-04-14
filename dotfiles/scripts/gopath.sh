#!/bin/bash
WD=$(pwd)
printf "\nGOPATH = %s\n" $WD

export GOPATH=$WD
export PATH=$PATH:$GOPATH/bin