//
//  FileIO.h
//  FileIO
//
//  Created by Taylor Shuler on 4/11/15.
//
//

#ifndef FILEIO
#define FILEIO

#include <list>
#include <string>

using std::list;
using std::ostream;
using std::string;

class FileIO {
public:
    // constructors
    
    /// default
    FileIO();
    
    /// copy
    FileIO(const FileIO &io);
    
    /// custom
    FileIO(string filename);
    
    // NOTE: Destructor not needed since the class attributes are terminated when they leave the scope
    
    // accessors
    string getFilename() const;
    list<string> getContent() const;
    
    // NOTE: Mutators not needed since their purpose is fulfilled by the custom constructor
    
    // operators
    FileIO& operator=(const FileIO &io);
    FileIO& operator+=(const FileIO &io);
    FileIO& operator-=(const FileIO &io);
    
    FileIO& operator+(const FileIO &io);
    FileIO& operator-(const FileIO &io);
    
    /// equality
    bool operator<(const FileIO &io) const;
    bool operator>(const FileIO &io) const;
    bool operator<=(const FileIO &io) const;
    bool operator>=(const FileIO &io) const;
    bool operator==(const FileIO &io) const;
    bool operator!=(const FileIO &io) const;
    
    friend ostream& operator<<(ostream& outstream, const FileIO &io);
    
    // member functions
    
    /// binds a new file to a FileIO object
    void setFile(string filename);
    
    /// writes the contents of the object to a file
    void write();
private:
    // error handling
    void panic(const string &msg);
    
    // attributes
    string filename;
    list<string> content;
};

#endif /* defined(FILEIO) */