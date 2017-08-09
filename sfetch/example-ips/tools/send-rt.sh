#!/bin/bash

datetime=$(date +%Y-%m-%dT%H:%M:%S)
duration=24

echo nc -w 1 ${IPS_ORT_SERVER_HOST} 8888 IPS ${IPS_CUSTOMER} ${IPS_DEVICE} realtime $datetime $duration
echo IPS ${IPS_CUSTOMER} ${IPS_DEVICE} realtime $datetime $duration | nc -w 1 ${IPS_ORT_SERVER_HOST} 8888
