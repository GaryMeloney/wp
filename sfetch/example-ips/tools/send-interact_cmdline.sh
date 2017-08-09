#!/bin/bash

echo IPS $1 $2 interact | nc -w 1 mylistdogstaging.cloudapp.net 8888
