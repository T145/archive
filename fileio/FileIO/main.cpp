//
//  main.cpp
//  FileIO
//
//  Created by Taylor Shuler on 4/11/15.
//
//

#include "FileIO.h"

#include <iostream>

using std::cout;

int main() {
    FileIO a("test1.txt"), b("test2.txt"), c;
    c = a + b;
    cout << c;
}