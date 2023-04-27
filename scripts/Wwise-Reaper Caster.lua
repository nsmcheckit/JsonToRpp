-------------------------------------------------------------------------------------------
------------------------------------GUI Part----------------------------------------------
------------------------------------------------------------------------------------------
local reaper = reaper

if not reaper.APIExists("ImGui_CreateContext") then
  reaper.MB("Please install ReaImGui to use this script.", "ReaImGui not found", 0)
  return
end

local ctx = reaper.ImGui_CreateContext("wwise-reaperGUI")  
local window_flags=  reaper.ImGui_WindowFlags_NoCollapse()| reaper.ImGui_WindowFlags_AlwaysAutoResize()
local txt_flags= reaper.ImGui_InputTextFlags_EnterReturnsTrue()
local open = true
local goname=""

local function GUILOOP()
  if open then
    local StartCapture=0
    local EndCapture=0
    local visible=true
    local open, visible = reaper.ImGui_Begin(ctx, "ReaWwise_Caster", open, window_flags)
    reaper.ImGui_Text( ctx,"press ENTER after input")
    local isinput,inputtxt=reaper.ImGui_InputText(ctx, "name your GO", inputtxt, txt_flags )
    
    if isinput then
      goname=inputtxt
      reaper.ShowMessageBox("GO Name Changed!!".."\n".."\n".."Click INIT if you want to change your GO name","ReaWwise_Caster Info",0)
    end
    
    reaper.ImGui_Text( ctx,"Your GO Name is: "..goname)   
    
    if reaper.ImGui_Button(ctx, "init") then
        reaper.AK_Waapi_Connect("127.0.0.1", 8080)
        --initrailize variables
        local currentitem=1
        --collect info
        sorted_track=GETMSG()
        wwiseevent=WWISEINIT(goname,sorted_track)
        
        if wwiseevent==nil then
          reaper.ShowMessageBox("Init Failed!!!","ReaWwise_Caster Info", 0)
        else
           reaper.ShowMessageBox("Init Success!!","ReaWwise_Caster Info", 0)
        end
          
    end
        
    if reaper.ImGui_Button(ctx, "start capture") then
      if sorted_track~= nil then
        reaper.AK_Waapi_Call("ak.wwise.core.profiler.startCapture",reaper.AK_AkJson_Map(),reaper.AK_AkJson_Map())
        reaper.ShowMessageBox("Capture Start","ReaWwise_Caster Info", 0)
        TRIGGER()
      else
         reaper.ShowMessageBox("Please Init First!!","ReaWwise_Caster Info",0)
      end
    end
        
    if reaper.ImGui_Button(ctx, "end capture") then
      needstoop=1
      reaper.ShowMessageBox("Capture Stopped","ReaWwise_Caster Info", 0)
      reaper.AK_Waapi_Call("ak.wwise.core.profiler.stopCapture",reaper.AK_AkJson_Map(),reaper.AK_AkJson_Map())
      reaper.ClearConsole()
    end
        
            
    if visible==false then
      reaper.ImGui_End(ctx)
      reaper.AK_Waapi_Call("ak.wwise.core.profiler.stopCapture",reaper.AK_AkJson_Map(),reaper.AK_AkJson_Map())
      needstoop=1
      reaper.ClearConsole()
      return
    end
   
    reaper.ImGui_End(ctx)
  else
    reaper.ImGui_DestroyContext(ctx)
  end
  reaper.defer(GUILOOP)
end
    


-------------------------------------------------------------------------------------------
------------------------------------MSG Part----------------------------------------------
------------------------------------------------------------------------------------------
function GETMSG()
  --get tracks--------------------
  local track = {}
  for i = 0,  reaper.CountSelectedTracks(0) - 1 do
    local curtrack=reaper.GetSelectedTrack(0, i)
    if reaper.GetMediaTrackInfo_Value( curtrack, "B_MUTE") == 0 then
      track[i] = 
      {
      track=curtrack,
      numitems=reaper.CountTrackMediaItems(curtrack),
      takeinfo={}
      }
    end
  end
  local numtracks=#track
  
  --get tracks info-----------------
  local myArray={}
  for j=0, numtracks do
    local numitems=track[j]["numitems"]
    local currenttrack=track[j]["track"]
    for i=0, numitems-1 do
      local take_info={}
      local item= reaper.GetTrackMediaItem(currenttrack, i)
      if reaper.GetMediaItemInfo_Value( item, "B_MUTE" )==0 then
        local take= reaper.GetMediaItemTake(item, 0)
        local test,name =reaper.GetSetMediaItemTakeInfo_String(take,"P_NAME",1,false)
        local itemstart= reaper.GetMediaItemInfo_Value(item, "D_POSITION")
        local itemend= itemstart+ reaper.GetMediaItemInfo_Value(item, "D_LENGTH")
       
        --whether has the same start item
        if not myArray[itemstart] then
          myArray[itemstart]={}
        end  
        if myArray[itemstart]~= nil then
          --if has, put take name into it
          table.insert(myArray[itemstart],name)
        end
        
        take_info[i]=
          {
          take_item = item,
          take_take = take,
          take_name = name,
          take_start = itemstart, 
          take_end = itemend
          }
        table.insert(track[j].takeinfo,take_info[i])
      end
    end
  end
  
  --arrange array----------------
  local sorted_track={}
  local index=0
  for k,v in pairs(myArray) do
    table.insert(sorted_track,{time= k, names=v})
  end
  table.sort(sorted_track,function(a,b) return a.time< b.time end)
  for i, v in ipairs(sorted_track) do
    v.index=i
  end
  return sorted_track
end

-------------------------------------------------------------------------------------------
------------------------------------Wwise Part----------------------------------------------
------------------------------------------------------------------------------------------

function WWISEINIT(goname,sorted_track)
  local numitems=#sorted_track
  --register game object
  local arg_gameobj=reaper.AK_AkJson_Map()
  local gameobj_name=reaper.AK_AkVariant_String(goname)
  local gameobj_id=reaper.AK_AkVariant_Int(111)
  reaper.AK_AkJson_Map_Set(arg_gameobj,"gameObject",gameobj_id)
  reaper.AK_AkJson_Map_Set(arg_gameobj,"name",gameobj_name)
  reaper.AK_Waapi_Call("ak.soundengine.registerGameObj",arg_gameobj,reaper.AK_AkJson_Map())
    
  --register listener gameobj
  local arg_listener=reaper.AK_AkJson_Map()
  local listener_id=reaper.AK_AkVariant_Int(222)
  local listener_name=reaper.AK_AkVariant_String("Listener")
  reaper.AK_AkJson_Map_Set(arg_listener,"gameObject",listener_id)
  reaper.AK_AkJson_Map_Set(arg_listener,"name",listener_name)
  local result=reaper.AK_Waapi_Call("ak.soundengine.registerGameObj",arg_listener,reaper.AK_AkJson_Map())
    
  --create listener
  local listener_array=reaper.AK_AkJson_Array()
  reaper.AK_AkJson_Array_Add(listener_array, listener_id)
  local arg_listener_creator=reaper.AK_AkJson_Map()
  reaper.AK_AkJson_Map_Set(arg_listener_creator,"emitter",gameobj_id)
  reaper.AK_AkJson_Map_Set(arg_listener_creator,"listeners",listener_array)
  reaper.AK_Waapi_Call("ak.soundengine.setListeners", arg_listener_creator, reaper.AK_AkJson_Map())
  
  --transfer item name to wwise event map
  local wwiseevent={}
  for i=1, numitems do
    wwiseevent[i]={}
    for k,v in pairs(sorted_track[i]["names"]) do
      local event=reaper.AK_AkVariant_String(v)
      local arg_eventgo=reaper.AK_AkJson_Map()
      reaper.AK_AkJson_Map_Set(arg_eventgo,"event",event)
      reaper.AK_AkJson_Map_Set(arg_eventgo,"gameObject",gameobj_id)
      table.insert(wwiseevent[i],arg_eventgo)
    end
  end
  return wwiseevent
end

-------------------------------------------------------------------------------------------
------------------------------------Trigger Part----------------------------------------------
------------------------------------------------------------------------------------------


function TRIGGER()
  isplay=reaper.GetPlayState()
  numitems=#sorted_track
  --get pos
  cursorpos=reaper.GetPlayPosition()
  --which item does cursorpos at
  if cursorpos< sorted_track[1]["time"] then
    currentitem=1
  end
  
  if cursorpos> sorted_track[numitems]["time"] then
    currentitem=numitems
  end
  
  for i=2, numitems do
    if cursorpos< sorted_track[i]["time"] and cursorpos>= sorted_track[i-1]["time"] then
      currentitem=i-1
    end
  end
  
  if isplay==1 then
    hasplayed=0
    canplay=0
    --check whether cursor has passing the start pos
    local curtime= sorted_track[currentitem]["time"]
    for k,v in pairs(tri_mark) do
      if currentitem== v then
        hasplayed=1
        break
      else
        canplay=1
      end
    end
    
    if cursorpos  >= curtime and cursorpos<curtime+0.1 and hasplayed~=1 and canplay==1 then
      --send trigger
      for k,v in pairs(wwiseevent[currentitem]) do
        
          local result=reaper.AK_Waapi_Call("ak.soundengine.postEvent",v,reaper.AK_AkJson_Map())
          local status=reaper.AK_AkJson_GetStatus(result)
          if status then
            table.insert(tri_mark,currentitem)
            canplay=0
          end
        
      end   
    end
  else 
    tri_mark={0}
    currentitem=0
    hasplayed=0
    canplay=0
  end
  if needstoop==1 then 
    return
  else
    reaper.defer(TRIGGER)
  end  
end
      
function exit()
  local arg_gameobj_end=reaper.AK_AkJson_Map()
  local arg_listener_end=reaper.AK_AkJson_Map()
  reaper.AK_AkJson_Map_Set(arg_gameobj_end,"gameObject",reaper.AK_AkVariant_Int(111))
  reaper.AK_AkJson_Map_Set(arg_listener_end,"gameObject",reaper.AK_AkVariant_Int(222))
  
  reaper.AK_Waapi_Call("ak.soundengine.unregisterGameObj",arg_gameobj_end, reaper.AK_AkJson_Map())
  reaper.AK_Waapi_Call("ak.soundengine.unregisterGameObj",arg_listener_end, reaper.AK_AkJson_Map())
  reaper.AK_Waapi_Disconnect()
  
  reaper.AK_AkJson_ClearAll()
end




-------------------------------------------------------------------------------------------
------------------------------------Main Part----------------------------------------------
------------------------------------------------------------------------------------------
GUILOOP()
reaper.atexit(exit)
return

