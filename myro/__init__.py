"""
Myro Base Classes.
(c) 2006, Institute for Personal Robots in Education
http://www.roboteducation.org/
Distributed under a Shared Source License
"""

__REVISION__ = "$Revision$"
__BUILD__    = "$Build: 0 $"
__VERSION__  = "1.0." + __BUILD__.split()[1]
__AUTHOR__   = "Doug Blank <dblank@cs.brynmawr.edu>"

from idlelib import PyShell
import sys, atexit, time, random, pickle, thread
import myro.globvars
from myro.media import *
from myro.speech import *
from myro.chat import *

try:
    import Tkinter
    import tkFileDialog
    import tkColorChooser
    import Dialog
except:
    Tkinter = None
if Tkinter != None:
    #from myro.graphics import *
    from myro.widgets import AskDialog as _AskDialog
    try:
        myro.globvars.gui = Tkinter.Tk()
        myro.globvars.gui.withdraw()
    except:
        Tkinter = None
try:
    import tkSnack
    tkSnack.initializeSnack(myro.globvars.gui)
except:
    tkSnack = None

def _update_gui():
    if "flist" in dir(PyShell):
        PyShell.flist.pyshell.write("")
        #PyShell.flist.pyshell.update()

def wait(seconds):
    """
    Wrapper for time.sleep() so that we may later overload.
    """
    return time.sleep(seconds)

def currentTime():
    """
    Returns current time in seconds since 
    """
    return time.time()

def flipCoin():
    """
    Randomly returns "heads" or "tails".
    """
    return ("heads", "tails")[random.randrange(2)]

def randomNumber():
    """
    Returns a number between 0 (inclusive) and 1 (exclusive).
    """
    return random.random()

def askQuestion(question, answers = ["Yes", "No"], title = "Myro Question",
                default = 0, bitmap=Dialog.DIALOG_ICON):
    """ Displays a question and returns answer. """
    d = Dialog.Dialog(title=title, default=default, bitmap=bitmap,
                      text=question, strings=answers)
    return answers[int(d.num)]

def pickAFolder():
    folder = tkFileDialog.askdirectory()
    if folder == '':
        folder = myro.globvars.mediaFolder
    return folder
	
def pickAFile():
    path = tkFileDialog.askopenfilename(parent=myro.globvars.gui)
    return path

def pickAColor():
    color = tkColorChooser.askcolor()
    newColor = Color(color[0][0], color[0][1], color[0][2])
    return newColor

def ask(item, useCache = 0):
    retval = _ask(item, useCache = useCache)
    if len(retval.keys()) == 1:
        return retval[item]
    if len(retval.keys()) == 2 and "ok" in retval.keys():
        return retval[item]
    else:
        return retval

def _ask(data, title = "Information Request", forceAsk = 0, forceConsole = 0, useCache = 1):
    """ Given a dictionary return dictionary with answers. """
    if type(data) in [str]:
        data = {data:""}
    if type(data) in [list, tuple]:
        newData = {}
        for item in data:
            newData[item] = ""
        data = newData
    if useCache:
        # get data, if in cache:
        needToAsk = 0
        for question in data.keys():
            if question in myro.globvars.askData.keys():
                data[question] = myro.globvars.askData[question]
            else:
                needToAsk = 1
    else:
        needToAsk = 1
    # if I got it all, and don't need to ask, return
    # else, ask it all:
    if needToAsk or forceAsk: 
        if Tkinter == None or forceConsole:
            _askConsole(data, title)
        else:
            data = _askGUI(data, title)
            if data["ok"] == 0:
                raise KeyboardInterrupt
        # cache data in globals:
        for text in data.keys():
            myro.globvars.askData[text] = data[text]
    return data

def _askGUI(qlist, title = "Information Request"):
   d = _AskDialog(myro.globvars.gui, title, qlist)
   d.top.bind("<Return>", lambda event: d.OkPressed())
   ok = d.Show()
   if ok:
      retval = {"ok": 1}
      for name in qlist.keys():
          retval[name] = d.textbox[name].get()
      d.DialogCleanup()
      return retval
   else:
      d.DialogCleanup()
      return {"ok" : 0}

def _askConsole(data, title = "Information Request"):
    print "+-----------------------------------------------------------------+"
    print "|" + title.center(65) + "|"
    print "+-----------------------------------------------------------------+"
    print "| Please enter the following information. Default values are in   |"
    print "| brackets. To accept default values, just press enter.           |"
    print "------------------------------------------------------------------"
    for key in data.keys():
        retval = raw_input("   " + key + (" [%s]" % data[key])+ ": ")
        retval.strip() # remove any spaces on either side
        if retval != "":
            data[key] = retval
    return data


class BackgroundThread(threading.Thread):
    """
    A thread class for running things in the background.
    """
    def __init__(self, function, pause = 0.01):
        """
        Constructor, setting initial variables
        """
        self.function = function
        self._stopevent = threading.Event()
        self._sleepperiod = pause
        threading.Thread.__init__(self, name="MyroThread")
        
    def run(self):
        """
        overload of threading.thread.run()
        main control loop
        """
        while not self._stopevent.isSet():
            self.function()
            #self._stopevent.wait(self._sleepperiod)

    def join(self,timeout=None):
        """
        Stop the thread
        """
        self._stopevent.set()
        threading.Thread.join(self, timeout)

class Robot(object):
    _app = None
    _joy = None
    def __init__(self):
        """
        Base robot class.
        """
        self.services = {}
        if tkSnack != None:
            self.addService("computer.audio", "type", "tksnack")
        if Tkinter != None:
            self.addService("computer.graphics", "type", "tkinter")
        if myro.globvars.tts != None:
            self.addService("computer.text-to-speech", "type", str(myro.globvars.tts))

    def initializeRemoteControl(self, password):
        self.chat = Chat(self.name, password)

    def processRemoteControlLoop(self, threaded = 1):
        if threaded:
            self.thread = BackgroundThread(self.processRemoteControl, 1) # seconds
            self.thread.start()
        else:
            while 1:
                self.processRemoteControl()

    def processRemoteControl(self):
        messages = self.chat.receive()
        #print "process", messages
        for _from, message in messages:
            if message.startswith("robot."):
                # For user IM messages
                #print ">>> self." + message[6:]
                retval = eval("self." + message[6:])
                name, domain = _from.split("@")
                #print "sending:", pickle.dumps(retval)
                self.chat.send(name.lower(), pickle.dumps(retval))

    def addService(self, name, attribute, value):
        if name not in self.services.keys():
            self.services[name] = {}
        if attribute not in self.services[name]:
            self.services[name][attribute] = []
        self.services[name][attribute].append(value)
    
    def translate(self, amount):
        raise AttributeError, "this method needs to be written"

    def rotate(self, amount):
        raise AttributeError, "this method needs to be written"

    def move(self, translate, rotate):
        raise AttributeError, "this method needs to be written"

    def beep(self, duration, frequency1, frequency2 = None):
        if tkSnack != None:
            snd1 = tkSnack.Sound()
            filt1 = tkSnack.Filter('generator', frequency1, 30000,
                                   0.0, 'sine', int(11500*duration))
            if frequency2 != None:
                snd2 = tkSnack.Sound()
                filt2 = tkSnack.Filter('generator', frequency2, 30000,
                                       0.0, 'sine', int(11500*duration))
                map2 = tkSnack.Filter('map', 1.0)
                snd2.stop()
                # blocking is choppy; sleep below
                snd2.play(filter=filt2, blocking=0) 
            snd1.stop()
            # blocking is choppy; sleep below
            map1 = tkSnack.Filter('map', 1.0)
            snd1.play(filter=filt1, blocking=0)
            start = time.time()
            while time.time() - start < duration:
                myro.globvars.gui.update()
                time.sleep(.001)
        elif Tkinter != None:
            myro.globvars.gui.bell()            
            time.sleep(duration)
	else:
	    print "beep!", chr(7)
            time.sleep(duration)
        time.sleep(.1) # simulated delay, like real robot

    def update(self):
        raise AttributeError, "this method needs to be written"

### The rest of these methods are just rearrangements of the above

    def getVersion(self):
        return self.get("version")

    def getLight(self, *position):
        return self.get("light", *position)

    def getIR(self, *position):
        return self.get("ir", *position)

    def getLine(self, *position):
        return self.get("line", *position)

    def getStall(self):
        return self.get("stall")

    def getName(self):
        return self.get("name")

    def getAll(self):
        return self.get("all")

    def setLED(self, position, value):
        return self.set("led", position, value)
        
    def setName(self, name):
        return self.set("name", name)

    def setVolume(self, value):
        return self.set("volume", value)

    def setStartSong(self, songName):
        return self.set("startsong", songName)

    def joyStick(self):
        from myro.joystick import Joystick
	try:
	    import idlelib
	except:
	    idlelib = None
        if self._joy == None:
            self._joy = Joystick(parent = self._app, robot = self)
            thread.start_new_thread(self._joy.mainloop, ())
        else:
            self._joy.deiconify()

    def turn(self, direction, value = .8):
        if type(direction) in [float, int]:
            return self.rotate(direction)
        else:
            direction = direction.lower()
            if direction == "left":
                return self.turnLeft(value)
            elif direction == "right":
                return self.turnRight(value)
            elif direction in ["straight", "center"]:
                return self.rotate(0)

    def forward(self, amount):
        return self.translate(amount)

    def backward(self, amount):
        return self.translate(-amount)

    def turnLeft(self, amount):
        return self.rotate(amount)
    
    def turnRight(self, amount):
        return self.rotate(-amount)

    def stop(self):
        return self.move(0, 0)

    def motors(self, left, right):
        trans = (right + left) / 2.0
        rotate = (right - left) / 2.0
        return self.move(trans, rotate)

    def restart(self):
        pass
    def close(self):
        pass
    def open(self):
	pass
    def playSong(self, song, wholeNoteDuration = .545):
        """ Plays a song (list of note names, durations) """
        # 1 whole note should be .545 seconds for normal
        for tuple in song:
            self.playNote(tuple)

    def playNote(self, tuple, wholeNoteDuration = .545):
        if len(tuple) == 2:
            (freq, dur) = tuple
            self.beep(dur * wholeNoteDuration, freq)
        elif len(tuple) == 3:
            (freq1, freq2, dur) = tuple
            self.beep(dur * wholeNoteDuration, freq1, freq2)

from myro.robot.scribbler import Scribbler
from myro.robot.surveyor import Surveyor
from myro.robot.simulator import SimScribbler

class Computer(Robot):
    """ An interface to computer devices. """
    def __init__(self):
        """ Constructs a computer object. """
        Robot.__init__(self)
        if tkSnack:
            self.addService("audio", "type", "tksnack")
    def move(self, translate, rotate):
        """ Moves the robot translate, rotate velocities. """
        print "move(%f, %f)" % (translate, rotate)
    def speak(self, message, async = 1):
        """ Speaks a text message. """
        if myro.globvars.tts != None:
            myro.globvars.tts.speak(message, async)
        else:
            print "Text-to-speech is not loaded"
    def stopSpeaking(self):
        if myro.globvars.tts != None:
            myro.globvars.tts.stop()
        else:
            print "Text-to-speech is not loaded"
    def setVoice(self, name):
        if myro.globvars.tts != None:
            myro.globvars.tts.setVoice(name)
        else:
            print "Text-to-speech is not loaded"
    def getVoice(self):
        if myro.globvars.tts != None:
            return str(myro.globvars.tts.getVoice())
        else:
            print "Text-to-speech is not loaded"
    def getVoices(self):
        if myro.globvars.tts != None:
            return map(str, myro.globvars.tts.getVoices())
        else:
            print "Text-to-speech is not loaded"
    def playSpeech(self, filename):
        if myro.globvars.tts != None:
            myro.globvars.tts.playSpeech(filename)
        else:
            print "Text-to-speech is not loaded"
    def saveSpeech(self, message, filename):
        if myro.globvars.tts != None:
            myro.globvars.tts.saveSpeech(message, filename)
        else:
            print "Text-to-speech is not loaded"
            
computer = Computer()

# functions:
def _cleanup():
    if myro.globvars.robot != None:
        myro.globvars.robot.stop() # hangs?
	time.sleep(.5)
        myro.globvars.robot.close()
    if myro.globvars.simulator != None:
       myro.globvars.simulator.destroy()

# Get ready for user prompt; set up environment:
if not myro.globvars.setup:
    myro.globvars.setup = 1
    atexit.register(_cleanup)
    # Ok, now we're ready!
    print >> sys.stderr, "Myro, (c) 2006 Institute for Personal Robots in Education"
    print >> sys.stderr, "[See http://www.roboteducation.org/ for more information]"
    print >> sys.stderr, "Version %s, Revision %s, ready!" % (__VERSION__, __REVISION__.split()[1])

## Functional interface:

def requestStop():
    if myro.globvars.robot:
        myro.globvars.robot.requestStop = 1
def initialize(id = None):
    myro.globvars.robot = Scribbler(id)
def simulator(id = None):
    myro.globvars.robot = SimScribbler(id)
def translate(amount):
    if myro.globvars.robot:
        return myro.globvars.robot.translate(amount)
def rotate(amount):
    if myro.globvars.robot:
        return myro.globvars.robot.rotate(amount)
def move(translate, rotate):
    if myro.globvars.robot:
        return myro.globvars.robot.move(translate, rotate)
def forward(amount):
    if myro.globvars.robot:
        return myro.globvars.robot.forward(amount)
def backward(amount):
    if myro.globvars.robot:
        return myro.globvars.robot.backward(amount)
def turn(direction, amount = .8):
    if myro.globvars.robot:
        return myro.globvars.robot.turn(direction, amount)
def turnLeft(amount):
    if myro.globvars.robot:
        return myro.globvars.robot.turnLeft(amount)
def turnRight(amount):
    if myro.globvars.robot:
        return myro.globvars.robot.turnRight(amount)
def stop():
    if myro.globvars.robot:
        return myro.globvars.robot.stop()
def openConnection():
    if myro.globvars.robot:
        return myro.globvars.robot.open()
def closeConnection():
    if myro.globvars.robot:
        return myro.globvars.robot.close()
def get(sensor = "all", *pos):
    if myro.globvars.robot:
        return myro.globvars.robot.get(sensor, *pos)
def getVersion():
    if myro.globvars.robot:
        return myro.globvars.robot.get("version")
def getLight(*pos):
    if myro.globvars.robot:
        return myro.globvars.robot.get("light", *pos)
def getIR(*pos):
    if myro.globvars.robot:
        return myro.globvars.robot.get("ir", *pos)
def getLine(*pos):
    if myro.globvars.robot:
        return myro.globvars.robot.get("line", *pos)
def getStall():
    if myro.globvars.robot:
        return myro.globvars.robot.get("stall")
def getAll():
    if myro.globvars.robot:
        return myro.globvars.robot.get("all")
def getName():
    if myro.globvars.robot:
        return myro.globvars.robot.get("name")
def getStartSong():
    if myro.globvars.robot:
        return myro.globvars.robot.get("startsong")
def getVolume():
    if myro.globvars.robot:
        return myro.globvars.robot.get("volume")
def update():
    if myro.globvars.robot:
        return myro.globvars.robot.update()
def beep(duration, frequency1, frequency2 = None):
    if myro.globvars.robot:
        return myro.globvars.robot.beep(duration, frequency1, frequency2)
def set(item, position, value = None):
    if myro.globvars.robot:
        return myro.globvars.robot.set(item, position, value)
def setLED(position, value):
    if myro.globvars.robot:
        return myro.globvars.robot.set("led", position, value)
def setName(name):
    if myro.globvars.robot:
        return myro.globvars.robot.set("name", name)
def setVolume(value):
    if myro.globvars.robot:
        return myro.globvars.robot.set("volume", value)
def setStartSong(songName):
    if myro.globvars.robot:
        return myro.globvars.robot.set("startsong", songName)
def motors(left, right):
    if myro.globvars.robot:
        return myro.globvars.robot.motors(left, right)
def restart():
    if myro.globvars.robot:
        return myro.globvars.robot.restart()
def joyStick():
    if myro.globvars.robot:
        return myro.globvars.robot.joyStick()
def playSong(song, wholeNoteDuration = .545):
    if myro.globvars.robot:
        return myro.globvars.robot.playSong(song, wholeNoteDuration)
def playNote(tup, wholeNoteDuration = .545):
    if myro.globvars.robot:
        return myro.globvars.robot.playNote(tup, wholeNoteDuration)
# --------------------------------------------------------
# Error handler:
# --------------------------------------------------------
import traceback
def _myroExceptionHandler(etype, value, tb):
    # make a window
    #win = HelpWindow()
    lines = traceback.format_exception(etype, value, tb)
    print >> sys.stderr, "Myro is stopping: -------------------------------------------"
    for line in lines:
        print >> sys.stderr, line.rstrip()
sys.excepthook = _myroExceptionHandler
