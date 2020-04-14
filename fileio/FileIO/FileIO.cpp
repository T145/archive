//
//  FileIO.cpp
//  FileIO
//
//  Created by Taylor Shuler on 4/11/15.
//
//

#include "FileIO.h"

#include <fstream>
#include <iostream>

using std::endl;
using std::ifstream;
using std::ofstream;

using std::runtime_error;

FileIO::FileIO() : filename("null") {}

FileIO::FileIO(const FileIO &io) {
    setFile(io.getFilename());
}

FileIO::FileIO(string Filename) {
    setFile(Filename);
}

string FileIO::getFilename() const {
    return filename;
}

list<string> FileIO::getContent() const {
    return content;
}

FileIO& FileIO::operator=(const FileIO &io) {
    if (this == &io) {
        return *this;
    }
    
    // set the filename
    filename = io.getFilename();
    
    // create a deep copy of io's content
    for (const auto &line : io.getContent()) {
        content.push_back(line);
    }
    
    return *this;
}

FileIO& FileIO::operator+=(const FileIO &io) {
    for (const auto &line : io.getContent()) {
        content.push_back(line);
    }
    
    return *this;
}

FileIO& FileIO::operator-=(const FileIO &io) {
    for (const auto &line : io.getContent()) {
        content.remove(line);
    }
    
    return *this;
}

FileIO& FileIO::operator+(const FileIO &io) {
    for (const auto &line : io.getContent()) {
        content.push_back(line);
    }
    
    return *this;
}

FileIO& FileIO::operator-(const FileIO &io) {
    for (const auto &line : io.getContent()) {
        content.remove(line);
    }
    
    return *this;
}

bool FileIO::operator<(const FileIO &io) const {
    return content < io.content;
}

bool FileIO::operator>(const FileIO &io) const {
    return content > io.content;
}

bool FileIO::operator<=(const FileIO &io) const {
    return content <= io.content;
}

bool FileIO::operator>=(const FileIO &io) const {
    return content >= io.content;
}

bool FileIO::operator==(const FileIO &io) const {
    return content == io.content;
}

bool FileIO::operator!=(const FileIO &io) const {
    return content != io.content;
}

ostream& operator<<(ostream &outstream, const FileIO &io) {
    for (const auto &line : io.getContent()) {
        outstream << line << endl;
    }
}

void FileIO::setFile(string Filename) {
    // set the filename
    filename = Filename;
    
    ifstream in(filename);
    
    if (in) {
        // we have a file
        string line;
        while (getline(in, line)) {
            content.push_back(line);
        }
    } else {
        // we don't
        panic("No file exists with the name " + filename + "!");
    }
}

void FileIO::write() {
    ofstream out(filename);
    
    for (const auto &line : getContent()) {
        out << line << endl;
    }
}

void FileIO::panic(const string &msg) {
    throw runtime_error("ERROR: " + msg + "\n");
}