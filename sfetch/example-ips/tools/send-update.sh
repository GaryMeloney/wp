#!/bin/bash

echo nc -w 1 ${IPS_ORT_SERVER_HOST} 8888 IPS ${IPS_CUSTOMER} ${IPS_DEVICE} update
echo IPS ${IPS_CUSTOMER} ${IPS_DEVICE} update | nc -w 1 ${IPS_ORT_SERVER_HOST} 8888
