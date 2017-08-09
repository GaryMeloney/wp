import argparse
import BaseHTTPServer
import datetime
import json
import logging
import os
import platform
import psutil
import select
import signal
import socket
import subprocess
import sys
import threading
import time
import traceback

from logging.handlers import RotatingFileHandler
from threading import Timer

# c is for config; we use a single letter just to make accessing these variables easier
c={
  "IPS_VERSION":"0.0.19",
  "IPS_HTTP_HOST":"", # Symbolic name, meaning all available interfaces
  "IPS_HTTP_PORT":8080,

  "IPS_ORT_SERVER_HOST":"mylistdogstaging.cloudapp.net",
  "IPS_ORT_SERVER_PORT":3333,

  # These are directories either on server or local
  "IPS_SFETCH_BIN":"ips/vdp/*",
  "IPS_ROOT"      :"ips/customers",
  "IPS_CUSTOMER"  :"",
  "IPS_DEVICE"    :"",
  "IPS_SCHEDULE"  :"schedule",
  "IPS_CONTENT"   :"content",
  "IPS_DEFAULT"   :"default",
  "IPS_REALTIME"  :"rt",
  "IPS_LOG_DIR"   :"log",
  "IPS_LOG_FILE"  :"fetch.log",

  # Ability to tune the behavior
  "IPS_BROWSER"      :True,
  "IPS_BROWSER_KIOSK":True,
  "IPS_SMALL_VIDEO"  :False,

  "IPS_INDEX_HTML"      :"index.html",
  "IPS_INDEX_VIDEO_HTML":"index-video.html",

  "IPS_TOKEN":"IPS" # Poor man's known endpoint token
}

class Realtime:
  def __init__(self):
    self.lock=threading.Lock()
    self.isReady=False

  def fetchRealtime(self):
    remote="%s/%s/%s/%s"%(c["IPS_ROOT"],c["IPS_CUSTOMER"],c["IPS_DEVICE"],c["IPS_REALTIME"])
    local="%s"%(c["IPS_DEVICE"])
    fetchFile(remote,local)

    self.lock.acquire()
    self.isReady=True
    self.lock.release()

  def set(self,date,duration):
    self.lock.acquire()
    self.date=date
    self.duration=duration
    self.isReady=False
    self.lock.release()
    self.fetchRealtime()

  def isPlayingNow(self):
    now=datetime.datetime.now()
    start=self.date
    end=start+datetime.timedelta(seconds=int(self.duration))
    if now>=start and now<=end:
      return True
    else:
      return False

  def get(self):
    self.lock.acquire()

    if not self.isReady:
      self.lock.release()
      return None

    if not self.isPlayingNow():
      self.lock.release()
      return None

    res={}
    res["Content"]="%s/%s/%s"%(c["IPS_DEVICE"],c["IPS_REALTIME"],c["IPS_INDEX_HTML"])
    o=overlay.get()
    if not o is None:
      res["Overlay"]=o

    self.lock.release()
    return json.dumps(res)

# Create an instance of Realtime
realtime=Realtime()

class Overlay:
  def __init__(self):
    self.lock=threading.Lock()
    self.isReady=False

  def set(self,date,duration,overlay):
    self.lock.acquire()
    self.date=date
    self.duration=duration
    self.overlay=overlay
    self.isReady=True
    self.lock.release()

  def isPlayingNow(self):
    now=datetime.datetime.now()
    start=self.date
    end=start+datetime.timedelta(seconds=int(self.duration))
    if now>=start and now<=end:
      return True
    else:
      return False

  def get(self):
    self.lock.acquire()

    if not self.isReady:
      self.lock.release()
      return None

    if not self.isPlayingNow():
      self.lock.release()
      return None

    o=self.overlay
    self.lock.release()
    return o

# Create an instance of Overlay
overlay=Overlay()

class Schedule:
  def __init__(self):
    self.schedule=[]
    self.lock=threading.Lock() # Protect the json data between get/set

  # This is for the sort algorithm
  def extract_time(self,item):
    try:
      return datetime.datetime.strptime(item["Start"],"%Y-%m-%dT%H:%M:%S")
    except KeyError:
      return 0

  # Set the schedule to include only the items that are playing now or later
  # Concurrency assumption- caller already has the lock
  def removeOld(self):
    popme=[]
    now=datetime.datetime.now()
    i=0
    for s in self.schedule:
      start=datetime.datetime.strptime(s["Start"],"%Y-%m-%dT%H:%M:%S")
      end=start+datetime.timedelta(seconds=int(s["Duration"]))
      if end<now:
        # Keep a list of indicies to remove
        popme.append(i)
      elif start>=now:
        # No need to continue walking the schedule since everything from here on out is later
        break
      i+=1

    i=0
    for p in popme:
      # Remove this old item
      self.schedule.pop(p-i)

      # Must account for the length of the list changing from the previous pop
      i+=1

  # Concurrency assumption- caller already has the lock
  def display(self):
    logging.debug("Current schedule:")
    for s in self.schedule:
      logging.debug(s)

  def set(self,s):
    self.lock.acquire()
    self.schedule=s
    self.removeOld()
    if len(self.schedule)>0:
      self.schedule.sort(key=self.extract_time,reverse=False)
    self.lock.release()

  def getScheduleItem(self):
    item=None
    now=datetime.datetime.now()

    self.lock.acquire()
    self.removeOld()
    for s in self.schedule:
      start=datetime.datetime.strptime(s["Start"],"%Y-%m-%dT%H:%M:%S")
      end=start+datetime.timedelta(seconds=int(s["Duration"]))
      if now>=start and now<=end:
        item=s
        break
      elif start>now:
        break
    self.lock.release()

    return item

  def formatResponse(self,content):
    res={}
    res["Content"]=content
    o=overlay.get()
    if not o is None:
      res["Overlay"]=o
    return json.dumps(res)

  def get(self):
    # Check if we need to override schedule with realtime
    rt=realtime.get()
    if not rt is None:
      return rt

    s=self.getScheduleItem()
    if not s==None and "Video" in s:
      # Show a blank screen behind omxplayer
      content=c["IPS_INDEX_VIDEO_HTML"]
    elif not s==None:
      # Schedule item
      content="%s/%s/%s/%s"%(c["IPS_DEVICE"],c["IPS_CONTENT"],str(s["Content"]),c["IPS_INDEX_HTML"])
    else:
      # Default content
      content="%s/%s/%s"%(c["IPS_DEVICE"],c["IPS_DEFAULT"],c["IPS_INDEX_HTML"])

    return self.formatResponse(content)

  def getVideo(self):
    content=None
    end=None
    now=datetime.datetime.now()
    self.lock.acquire()
    for s in self.schedule:
      if "Video" in s:
        start=datetime.datetime.strptime(s["Start"],"%Y-%m-%dT%H:%M:%S")
        end=start+datetime.timedelta(seconds=int(s["Duration"]))

        # Pad the start time so we don't see flicker
        pad=.5
        padstart=start-datetime.timedelta(seconds=pad)

        if now>=padstart and now<=end:
          video=str(s["Video"])
          content="%s/%s/%s/%s"%(c["IPS_DEVICE"],c["IPS_CONTENT"],str(s["Content"]),video)
          break
        elif start>now:
          # No need to run the whole list
          break
    self.lock.release()
    return content,end

# Create an instance of Schedule
schedule=Schedule()

class FetchTimer:
  def __init__(self):
    self.fetchTimer=None

  def startNow(self):
    self.start(0.1)

  def startLater(self):
    # Calculate how long before we fetch again
    now=datetime.datetime.now()
    runat=now.replace(hour=00,minute=00,second=01,microsecond=00)
    seconds=(runat-now).total_seconds()

    # seconds will be negative when the refresh time has already passed for today
    # In this case, just do it tomorrow by adding one day
    if seconds<=0:
      seconds+=60*60*24

    self.start(seconds)

  def start(self,seconds):
    m,s= divmod(seconds,60)
    h,m= divmod(m,60)
    logging.debug("Next schedule fetch in %d:%02d:%02d"%(h,m,s))

    self.fetchTimer=Timer(seconds,setScheduleFromServer)
    self.fetchTimer.start()

  def stop(self):
    if not self.fetchTimer is None and self.fetchTimer.is_alive():
      self.fetchTimer.cancel()
      self.fetchTimer.join()

# Create an instance of FetchTimer
fetchTimer=FetchTimer()

# This is the http server
class HttpServerHandler(BaseHTTPServer.BaseHTTPRequestHandler):
  protocol_version="HTTP/1.0"

  def do_GET(self):
    self.send_response(200)
    self.send_header("Content-type","text/plain")
    self.send_header("Access-Control-Allow-Origin","*")
    self.send_header("Access-Control-Allow-Headers","Content-Type,Authorization")
    self.send_header("Access-Control-Allow-Methods","GET,PUT,POST,DELETE")
    self.end_headers()
    response=schedule.get()
    self.wfile.write(response)
    logging.debug(response)

  def log_message(self, format, *args):
    #logging.debug(format%args)
    pass

def httpServerThread():
  logging.debug("HTTP Server Starts - %s:%s"%(socket.gethostname(),c["IPS_HTTP_PORT"]))

  # This is a blocking call; use shutdown() to stop
  httpd.serve_forever()

  # Shutdown the http server
  httpd.server_close()
  httpd.shutdown()
  httpd.socket.close()
  logging.debug("HTTP Server Stops - %s:%s"%(socket.gethostname(),c["IPS_HTTP_PORT"]))

def startHttpServer():
  t=threading.Thread(target=httpServerThread)
  t.start()
  time.sleep(.1) # This is here just so we print cleanly to output
  return t

def ortHandler(s):
  # Reboot command does not have any date or duration so we just check s
  if s=="reboot":
    # After switching to run under daemontools, we must issue this command
    #   with shell=True otherwise the operation fails (permissions issue?)
    logging.debug("Received reboot command")
    rebootcmd="sudo reboot"
    subprocess.call(rebootcmd,shell=True)
    return # Will never get this since we rebooted on previous instruction

  if s=="interact":
    # Offer an interactive mode for debugging; we need to free up the display.
    #    We do this by closing the browser and stopping the schedule manager.
    #    This will close omx player and never launch it again.
    logging.debug("Received interact command")
    browserCtrl.terminate()
    scheduleMgr.terminate()
    return

  if s=="update":
    # Remote FW update command
    logging.debug("Received firmware update command")
    fetchSfetch()
    logging.debug("sfetch is updated. Reboot now...")
    rebootcmd="sudo reboot"
    subprocess.call(rebootcmd,shell=True)
    return
    
  # Find the command, date, and duration by splitting the string
  try:
    cmd=s.split(" ")[0]
    date=s.split(" ")[1]
    date=datetime.datetime.strptime(date,"%Y-%m-%dT%H:%M:%S")
    duration=s.split(" ")[2]
  except:
    logging.debug("Error parsing data in command handler:%s"%s)
    return

  if cmd=="realtime":
    logging.debug("realtime details:")
    logging.debug("     date: %s"%(date.strftime("%Y-%m-%dT%H:%M:%S")))
    logging.debug(" duration: %s"%(duration))
    realtime.set(date,duration)

  if cmd=="overlay":
    i=s.index("{")
    o=s[i:]
    logging.debug("overlay details:")
    logging.debug("     date: %s"%(date.strftime("%Y-%m-%dT%H:%M:%S")))
    logging.debug(" duration: %s"%(duration))
    logging.debug("     json: %s"%o)
    o=json.loads(o)
    overlay.set(date,duration,o)

class ORTClient:
  def __init__(self):
    self.shutdown=False

  def setKeepAlive(self,sock,after_idle_sec=1,interval_sec=10,max_fails=5):
    # Enable keep-alive if running under Linux (rpi)
    if platform.system()=="Linux":
      sock.setsockopt(socket.SOL_SOCKET,socket.SO_KEEPALIVE,1)
      sock.setsockopt(socket.IPPROTO_TCP,socket.TCP_KEEPIDLE,after_idle_sec)
      sock.setsockopt(socket.IPPROTO_TCP,socket.TCP_KEEPINTVL,interval_sec)
      sock.setsockopt(socket.IPPROTO_TCP,socket.TCP_KEEPCNT,max_fails)

  def clientThread(self):
    logging.debug("ORT Client Starts - %s:%s"%(c["IPS_ORT_SERVER_HOST"],c["IPS_ORT_SERVER_PORT"]))

    s=None # Socket object

    while not self.shutdown:
      try:
        s=socket.create_connection((c["IPS_ORT_SERVER_HOST"],c["IPS_ORT_SERVER_PORT"]),.5)
        self.setKeepAlive(s)

        # Get the local IP address of this system and include 
        # in the connection string to the server for debugging ability
        localAddr=s.getsockname()[0]
        logging.debug("Local IP Address=%s"%localAddr)

        connectionString="%s %s %s %s"%(c["IPS_TOKEN"],c["IPS_CUSTOMER"],c["IPS_DEVICE"],localAddr)
        s.sendall(connectionString)
      except:
        logging.debug("%s:%s ORTService not available"%(c["IPS_ORT_SERVER_HOST"],c["IPS_ORT_SERVER_PORT"]))

        # Throttle back on retrying the connection; no need to spin on this
        time.sleep(1)
        continue

      while not self.shutdown:
        ready=select.select([s],[],[],.1)
        if ready[0]:
          try:
            data=s.recv(1024)
            if not data:
              logging.debug("%s:%s connection closed"%(c["IPS_ORT_SERVER_HOST"],c["IPS_ORT_SERVER_PORT"]))
              break
            else:
              ortHandler(data)
          except:
            logging.debug("%s:%s connection closed"%(c["IPS_ORT_SERVER_HOST"],c["IPS_ORT_SERVER_PORT"]))
            break

      if not self.shutdown:
        # Throttle back on retrying the connection; no need to spin on this
        time.sleep(1)

    if not s is None:
      s.shutdown(socket.SHUT_RDWR)
      s.close()
    logging.debug("ORT Client Stops - %s:%s"%(c["IPS_ORT_SERVER_HOST"],c["IPS_ORT_SERVER_PORT"]))

  def start(self):
    self.t=threading.Thread(target=self.clientThread)
    self.t.start()
    time.sleep(.1) # This is here just so we print cleanly to output

  def stop(self):
    self.shutdown=True

def prepareLocalDirs():
  dirname=c["IPS_DEVICE"]
  if not os.path.exists(dirname):
    os.makedirs(dirname)
      
  dirname="%s/%s"%(c["IPS_DEVICE"],c["IPS_SCHEDULE"])
  if not os.path.exists(dirname):
    os.makedirs(dirname)

  dirname="%s/%s"%(c["IPS_DEVICE"],c["IPS_CONTENT"])
  if not os.path.exists(dirname):
    os.makedirs(dirname)

  dirname="%s/%s"%(c["IPS_DEVICE"],c["IPS_DEFAULT"])
  if not os.path.exists(dirname):
    os.makedirs(dirname)

  dirname="%s/%s"%(c["IPS_DEVICE"],c["IPS_REALTIME"])
  if not os.path.exists(dirname):
    os.makedirs(dirname)

def fetchFileScp(remote,local):
  scp="scp"
  recurse="-r"
  port="-P 4022"
  username="listdog"
  server="mylistdogstaging.cloudapp.net"
  password="listdog1!"
  remotepath="%s@%s:%s"%(username,server,remote)
  subprocess.call([scp,recurse,port,remotepath,local])

def fetchFile(remote,local):
  username="listdog"
  server="mylistdogstaging.cloudapp.net"
  remotepath="%s@%s:%s"%(username,server,remote)
  logfile="--log-file=%s/%s.rsync.log"%(c["IPS_LOG_DIR"],remote.replace("/","-"))
  remoteshell="--rsh=ssh -p 4022"

  rsync=["rsync"]
  rsync.append("--archive")
  rsync.append("--verbose")
  rsync.append("--compress")
  rsync.append("--stats")
  rsync.append("--progress")
  rsync.append("--itemize-changes")
  rsync.append(remoteshell)
  rsync.append(logfile)
  rsync.append(remotepath)
  rsync.append(local)

  # Open file descriptors to /dev/null for stdout and stderr
  fdnullout=open(os.devnull,"w")
  fdnullerr=open(os.devnull,"w")

  try:
    subprocess.call(rsync,stdout=fdnullout,stderr=fdnullerr)
  except Exception,err:
    logging.debug("Error launching %s: %s"%(rsync,err))

  # Close file descriptors to /dev/null for stdout and stderr
  fdnullout.close()
  fdnullerr.close()

def getUnique(mylist):
  unique=set()
  for l in mylist:
    unique.add(str(l["Content"]))
  return unique

def fetchContent(filename):
  f=open(filename,"r")
  jschedule=json.load(f)

  for entry in getUnique(jschedule):
    remote="%s/%s/%s/%s"%(c["IPS_ROOT"],c["IPS_CUSTOMER"],c["IPS_CONTENT"],entry)
    local="%s/%s"%(c["IPS_DEVICE"],c["IPS_CONTENT"])
    logging.debug("Fetch content: %s"%remote)
    fetchFile(remote,local)

  # We set the schedule after downloading all the content
  schedule.set(jschedule)

def fetchSchedule():
  filename="%s.json"%time.strftime("%Y%m%d")
  remote="%s/%s/%s/%s/%s"%(c["IPS_ROOT"],c["IPS_CUSTOMER"],c["IPS_DEVICE"],c["IPS_SCHEDULE"],filename)
  local="%s/%s"%(c["IPS_DEVICE"],c["IPS_SCHEDULE"])
  logging.debug("Fetch schedule: %s"%remote)
  fetchFile(remote,local)
  filename="%s/%s/%s"%(c["IPS_DEVICE"],c["IPS_SCHEDULE"],filename)
  return filename

def fetchSfetch():
  remote=c["IPS_SFETCH_BIN"]
  local="."
  logging.debug("Fetch sfetch: %s"%remote)
  fetchFile(remote,local)

def fetchDefault():
  remote="%s/%s/%s/%s"%(c["IPS_ROOT"],c["IPS_CUSTOMER"],c["IPS_DEVICE"],c["IPS_DEFAULT"])
  local="%s"%(c["IPS_DEVICE"])
  logging.debug("Fetch default content: %s"%remote)
  fetchFile(remote,local)

def setScheduleFromServer():
  # Fetch default first so the client can play that as soon as it's ready
  fetchDefault()

  # Fetch the schedule file from head end
  filename=fetchSchedule()

  # Double check that we actually got a schedule
  if not os.path.isfile(filename):
    logging.debug("Schedule does not exist: %s"%filename)

    # Check again in 5 minutes
    fetchTimer.start(60*5)

    # Do not bother to fetch content; return from here
    return

  # Fetch the content based on the schedule
  fetchContent(filename)

  # Start the timer again for tomorrow
  fetchTimer.startLater()

def setScheduleFromLocal():
  filename="%s.json"%time.strftime("%Y%m%d")
  filename="%s/%s/%s"%(c["IPS_DEVICE"],c["IPS_SCHEDULE"],filename)
  logging.debug("Schedule: %s"%filename)
  f=open(filename,"r")
  jschedule=json.load(f)
  schedule.set(jschedule)

def terminateProcess(proc):
  try:
    # Get the PID, exe, and children
    procpid=proc.pid
    exe=psutil.Process(procpid)
    children=exe.children(recursive=True)
  except psutil.NoSuchProcess:
    logging.debug("terminateProcess: exception psutil.NoSuchProcess")

  # Terminate the parent first, and it should clean up all its children
  try:
    proc.terminate()
  except:
    logging.debug("terminateProcess: exception omxProc.terminate()")

  # Terminate the children if any exist
  # This sometimes happens with omxplayer.bin
  for process in children:
    try:
      process.terminate()
    except:
      logging.debug("terminateProcess: exception omxProc.terminate()")

class ScheduleMgr():
  def __init__(self):
    self.stop=None

  def start(self):
    self.stop=threading.Event()
    self.mythread=threading.Thread(target=self.scheduleThread)
    self.mythread.start()
    time.sleep(.1) # This is here just so we print cleanly to output

  def scheduleThread(self):
    logging.debug("Schedule Manager Starts")

    while not self.stop.is_set():
      # Check if we need to override schedule with realtime
      rt=realtime.get()
      if not rt is None:
        time.sleep(.1)
        continue

      video,end=schedule.getVideo()
      if not video==None:
        pad=.5
        now=datetime.datetime.now()
        end+=datetime.timedelta(seconds=pad)
        duration=(end-now).total_seconds()

        # If the end time is near, skip it
        if duration<2:
          logging.debug("Do not play %s for %d seconds"%(video,duration))
          if duration>0:
            time.sleep(duration)
          continue

        logging.debug("Play %s for %d seconds"%(video,duration))
        omx=OmxCtrl(video)

        while not self.stop.is_set() and now<end:
          rt=realtime.get()
          if not rt is None:
            break
          time.sleep(.1)
          now=datetime.datetime.now()

        omx.terminate()
      else:
        time.sleep(.1)

    logging.debug("Schedule Manager Stops")

  def terminate(self):
    if not self.stop is None and not self.stop.is_set():
      self.stop.set()
      self.mythread.join()

# Create an instance of ScheduleMgr
scheduleMgr=ScheduleMgr()

class OmxCtrl():
  def __init__(self,video):
    self.video=video
    self.stop=threading.Event()
    self.mythread=threading.Thread(target=self.omxThread)
    self.mythread.start()
    time.sleep(.1) # This is here just so we print cleanly to output

  def omxThread(self):
    omxcmd=["/usr/bin/omxplayer"]
    omxcmd.append("--loop")
    omxcmd.append("--no-osd")
    omxcmd.append("--no-keys")
    #omxcmd.append("--genlog")
    omxcmd.append("-n") # Disable audio codec
    omxcmd.append("-1")
    if c["IPS_SMALL_VIDEO"]==True:
      omxcmd.append("--win")
      omxcmd.append("100 100 400 400")

    # A little hack, but we need to move the directory back one level
    # This is to account for running omxplayer from the log folder
    self.video="../%s"%self.video
    omxcmd.append(self.video)

    # Open file descriptors to /dev/null for stdout and stderr
    fdnullout=open(os.devnull,"w")
    fdnullerr=open(os.devnull,"w")

    logging.debug("omxThread: Start")
    while not self.stop.is_set():
      if platform.system()=="Linux":
        try:
          # Execute from log folder so the omxplayer log goes there
          # Must set close_fds=True or else we see listen port 8080 get owned by dbus-daemon
          omxProc=subprocess.Popen(omxcmd,cwd=c["IPS_LOG_DIR"],stdout=fdnullout,stderr=fdnullerr,close_fds=True)
        except Exception,err:
          logging.debug("Error launching %s: %s"%(omxcmd,err))
          self.stop.set()
          break

      while not self.stop.is_set():
        # Wait for schedule manager to shut us down
        time.sleep(.1)

      if platform.system()=="Linux":
        terminateProcess(omxProc)

      # Close file descriptors to /dev/null for stdout and stderr
      fdnullout.close()
      fdnullerr.close()

    logging.debug("omxThread: Stop")

  def terminate(self):
    if not self.stop.is_set():
      self.stop.set()
      self.mythread.join()

class BrowserCtrl():
  def __init__(self):
    self.stop=None

  def start(self):
    self.stop=threading.Event()
    self.mythread=threading.Thread(target=self.browserThread)
    self.mythread.start()
    time.sleep(.1) # This is here just so we print cleanly to output

  def browserThread(self):
    logging.debug("Browser Thread Starts")

    while not self.stop.is_set():
      if platform.system()=="Linux":
        browsercmd=["/usr/bin/chromium-browser"]
        browsercmd.append("--display=:0")
        browsercmd.append("--disable-session-crashed-bubble")
        browsercmd.append("--noerordialogs")
        browsercmd.append("--disable-infobars")
        browsercmd.append("--no-first-run")
        if c["IPS_BROWSER_KIOSK"]==True:
          browsercmd.append("--kiosk")
      else:
        browsercmd=["/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"]

      browsercmd.append(c["IPS_INDEX_HTML"])

      # Open file descriptors to /dev/null for stdout and stderr
      fdnullout=open(os.devnull,"w")
      fdnullerr=open(os.devnull,"w")

      try:
        p=subprocess.Popen(browsercmd,stdout=fdnullout,stderr=fdnullerr,close_fds=True)
      except Exception,err:
        logging.debug("Error launching %s: %s"%(browsercmd,err))
        self.stop.set()
        break

      while not self.stop.is_set():
        time.sleep(.1)

    # Shutdown the browser
    terminateProcess(p)

    # Close file descriptors to /dev/null for stdout and stderr
    fdnullout.close()
    fdnullerr.close()

    logging.debug("Browser Thread Stops")

  def terminate(self):
    if not self.stop is None and not self.stop.is_set():
      self.stop.set()
      self.mythread.join()

# Create an instance of BrowserCtrl
browserCtrl=BrowserCtrl()

def setVarS(name,value):
  c[name]=os.environ.get(name,value)
  logging.debug("%s=%s"%(name,c[name]))

def setVarB(name,value):
  c[name]=bool(os.environ.get(name,value))
  logging.debug("%s=%s"%(name,c[name]))

def checkOverride():
  setVarS("IPS_ORT_SERVER_HOST",c["IPS_ORT_SERVER_HOST"])
  setVarS("IPS_CUSTOMER",       c["IPS_CUSTOMER"])
  setVarS("IPS_DEVICE",         c["IPS_DEVICE"])
  setVarB("IPS_BROWSER",        c["IPS_BROWSER"])
  setVarB("IPS_BROWSER_KIOSK",  c["IPS_BROWSER_KIOSK"])
  setVarB("IPS_SMALL_VIDEO",    c["IPS_SMALL_VIDEO"])

def configLogging():
  LOG_FILENAME="%s/%s"%(c["IPS_LOG_DIR"],c["IPS_LOG_FILE"])
  MAX_LOG_SIZE=5*1024*1024 # 5MB

  # Create the log directory
  dirname=c["IPS_LOG_DIR"]
  if not os.path.exists(dirname):
    os.makedirs(dirname)

  # File handler
  fh=logging.handlers.RotatingFileHandler(LOG_FILENAME,maxBytes=MAX_LOG_SIZE,backupCount=5)
  fh.setLevel(logging.DEBUG)

  # Console handler
  ch=logging.StreamHandler()
  ch.setLevel(logging.DEBUG)

  # Formatting
  formatter=logging.Formatter("%(asctime)s %(message)s")
  fh.setFormatter(formatter)
  ch.setFormatter(formatter)

  logger=logging.getLogger()
  logger.setLevel(logging.DEBUG)
  logger.addHandler(fh)
  logger.addHandler(ch)

class GracefulInterruptHandler(object):
  def __init__(self,signals=(signal.SIGINT,signal.SIGTERM,signal.SIGCONT)):
    self.signals=signals
    self.original_handlers={}

  def __enter__(self):
    self.interrupted=False
    self.released=False

    for sig in self.signals:
      self.original_handlers[sig]=signal.getsignal(sig)
      signal.signal(sig,self.handler)

    return self

  def handler(self,signum,frame):
    self.release()
    self.interrupted=True

  def __exit__(self,type,value,tb):
    self.release()

  def release(self):
    if self.released:
      return False

    for sig in self.signals:
      signal.signal(sig,self.original_handlers[sig])

    self.released=True
    return True

if __name__=="__main__":
  # Configure our logger
  configLogging()

  # Parse the commandline
  parser=argparse.ArgumentParser("python sfetch.py")
  parser.add_argument("-o","--omit-fetch",dest="omitfetch",default=False,action="store_true",
          help="Omit the lengthy fetch operation; assumes content is already local")
  parser.add_argument("-w","--wait",dest="startwait",default=0,
          help="Wait for STARTWAIT seconds before starting")
  parser.add_argument("-v","--version",dest="printversion",default=False,action="store_true",
          help="Print version number and exit")
  args=parser.parse_args()

  if args.printversion:
    print c["IPS_VERSION"]
    sys.exit()

  logging.debug("sfetch version=%s"%c["IPS_VERSION"])
  logging.debug("Commandline parameters or defaults:")
  logging.debug("omit-fetch=%s"%args.omitfetch)
  logging.debug("wait=%s"%args.startwait)

  # Allow the user to override local values with env vars
  logging.debug("Environment variables or defaults:")
  checkOverride()

  # Create local directories
  prepareLocalDirs()

  if args.startwait>0:
    logging.debug("Wait for %s seconds to allow system to boot..."%args.startwait)
    time.sleep(float(args.startwait))
    logging.debug("Finished waiting, time to move on.")

  # chmod this driver to resolve permissions issues
  # VCHIQ is a command interface between the running Linux kernel and peripherals (among other things) 
  # in the VideoCore silicon. /dev/vhciq provides generic userspace access to those commands for use by
  # (at minimum) the camera and audio subsystems as well. It's a decently dangerous interface to expose
  # to random programs, hence the somewhat restrictive permissions by default.
  if platform.system()=="Linux":
    vchiq_cmd=["sudo"]
    vchiq_cmd.append("chmod")
    vchiq_cmd.append("777")
    vchiq_cmd.append("/dev/vchiq")
    subprocess.call(vchiq_cmd)
    logging.debug("Change permissions on VCHIQ interface so omxplayer functions correctly")
    logging.debug(vchiq_cmd)

  # Create instance of http server and start it
  httpServerAddress=(c["IPS_HTTP_HOST"],c["IPS_HTTP_PORT"])
  httpd=BaseHTTPServer.HTTPServer(httpServerAddress,HttpServerHandler)
  httpServer=startHttpServer()

  # Start instance of overlay and realtime client
  ortClient=ORTClient()
  ortClient.start()

  # Start browser for HTML content
  if c["IPS_BROWSER"]==True:
    browserCtrl.start()

  # Start Schedule Manager for video content
  scheduleMgr.start()

  try:
    # Use this handler to catch signals ie shutdown
    with GracefulInterruptHandler() as sig_handler:
      if args.omitfetch:
        # Omit the lengthy fetch process; this is good for development/testing
        setScheduleFromLocal()
      else:
        # Fetch schedule and content from server; continue to do so once per day forever
        fetchTimer.startNow()

      while 1:
        # Wait for interrupt to cleanly shutdown
        time.sleep(.1) # 100ms
        if sig_handler.interrupted:
          logging.debug("Received shutdown interrupt")
          break
  except:
    logging.debug(traceback.format_exc())
  finally:
    # Shutdown the browser thread
    browserCtrl.terminate()

    # Shutdown the Schedule Manager
    scheduleMgr.terminate()

    # Cancel the fetch timer
    fetchTimer.stop()

    # Shutdown the HTTP server
    httpd.shutdown()
    httpServer.join()
    httpd=None

    # Shutdown the ORT Client
    ortClient.stop()

