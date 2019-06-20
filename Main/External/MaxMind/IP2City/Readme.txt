MaxMind File Encryption
-----------------------

The MaxMind license requires that their data files not be made publically available.
By default, LillTek GeoTracker servers will check for updates and download their
data from:

    http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat

This the encrypted GeoLite City or GeoIP City databases.  This file must be the
encrypted version of the file downloaded and extracted manually from MaxMind using
the following RSA key found in this folder:

    MaxMind.rsa.txt

Use the following VEGOMATIC command to encrypt the file.

    vegomatic crypto securefile encrypt @maxmind.rsa.txt -in:IP2City.dat -out:iP2City.encrypted.dat

