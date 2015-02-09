#!/bin/sh
if [ -d "/jffs2" ]
then
ifconfig eth1 192.168.0.2 netmask 255.255.255.0
ifconfig eth2 down
ifconfig eth3 down
mv /jffs2/usb_td /mnt/vfat
sync
/jffs2/atop_testd&

elif [ -d "/mnt/jffs2" ]
then
ifconfig eth1 192.168.0.2 netmask 255.255.255.0
ifconfig eth2 down
ifconfig eth3 down
mv /jffs2/usb_td /mnt/vfat
sync
/mnt/jffs2/atop_testd&
fi