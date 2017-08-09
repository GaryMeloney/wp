#!/bin/bash

datetime=$(date +%Y-%m-%dT%H:%M:%S)
duration=60
message="I am the soul"

overlay="{\"font-size\": \"80px\", \"text\": \"$message\", \"font-color\": \"red\", \"position\": \"middle\", \"font-family\": \"Arial\", \"speed\": \"slow\"}"

echo nc -w 1 ${IPS_ORT_SERVER_HOST} 8888 IPS ${IPS_CUSTOMER} ${IPS_DEVICE} overlay $datetime $duration $overlay
echo IPS ${IPS_CUSTOMER} ${IPS_DEVICE} overlay $datetime $duration $overlay | nc -w 1 ${IPS_ORT_SERVER_HOST} 8888
