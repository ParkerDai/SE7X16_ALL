export OPEN2000HOME=/jffs2/oracle/ns2000
export OPEN2000DIR=$OPEN2000HOME
export OPEN2000SRC=$OPEN2000HOME/src
export XAPPLRESDIR=$OPEN2000HOME/exe

export LD_LIBRARY_PATH=/usr/lib:/usr/local/lib:$OPEN2000DIR/exe:$LD_LIBRARY_PATH
export PATH=.:$PATH
export HOME=/users/oracle

#如果有SD卡，则设为FILE，否则为NONE
if [ -d /mnt/sd/ns2000 ] ; then
export HDB=FILE
else
export HDB=NONE
fi

alias	bye=logout
alias	run=source
alias 	exe='cd $OPEN2000DIR/exe'
alias	sys='cd $OPEN2000DIR/sys'
alias   data='cd $OPEN2000DIR/data'
alias	src='cd $OPEN2000DIR/src'
alias	fsrc='cd $OPEN2000DIR/front/src'
alias	pas='cd $OPEN2000DIR/src/pas'
alias   disp='export DISPLAY `who am i | cut -f2 -d"(" | cut -f1 -d")"`:0.0'
alias   e='exit'
alias   op='cd $OPEN2000DIR'

alias	h=history
alias	l='ls -ltr'
alias	rcp='rcp -pr'
alias   f='find . -depth -exec grep  \!* /dev/null {} \;'

alias 	pp='ps -eaf|grep '
