#!/bin/sh
if [ -d "/mnt/jffs2" ]
then
chmod 777 /mnt/jffs2/tcp_*
/mnt/jffs2/tcp_server485&

elif [ -d "/flash" ]
then
chmod 777 /flash/tcp_*
/flash/tcp_server485&

elif [ -d "/jffs2" ]
then
chmod 777 /jffs2/tcp_*
/jffs2/tcp_server485&
fi

