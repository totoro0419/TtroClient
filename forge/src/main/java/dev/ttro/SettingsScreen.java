package dev.ttro;
import com.google.gson.*;
import net.minecraft.client.gui.*;
import java.io.IOException;
import java.util.*;
import org.lwjgl.input.Keyboard;
import org.lwjgl.input.Mouse;

public final class SettingsScreen extends GuiScreen {
 private final GuiScreen parent;private String group="PvP",selected="crosshair";private int page,focus;private GuiTextField search;
 private final List<JsonObject> visible=new ArrayList<JsonObject>();private final Map<Integer,String> selectIds=new HashMap<Integer,String>();private final Map<Integer,String> paramIds=new HashMap<Integer,String>();
 private int listX,listWidth,contextX,contextWidth,rows;
 public SettingsScreen(GuiScreen p){parent=p instanceof net.minecraft.client.gui.inventory.GuiContainer?null:p;}
 @Override public void initGui(){String text=search==null?"":search.getText();search=new GuiTextField(999,fontRendererObj,20,53,Math.max(120,width-40),20);search.setEnableBackgroundDrawing(false);search.setTextColor(0xff343c45);search.setMaxStringLength(60);search.setText(text);build();}
 private void build(){buttonList.clear();visible.clear();selectIds.clear();paramIds.clear();rows=Math.max(1,(height-170)/25);
  buttonList.add(new FlatButton(1,20,18,80,22,"Back"));buttonList.add(new FlatButton(2,110,18,90,22,group));buttonList.add(new FlatButton(3,width-85,18,65,22,"Save"));
  for(JsonElement e:Config.catalog){JsonObject m=e.getAsJsonObject();if(m.get("group").getAsString().equals(group)&&m.get("name").getAsString().toLowerCase(Locale.ROOT).contains(search.getText().toLowerCase(Locale.ROOT)))visible.add(m);}
  page=Math.min(page,Math.max(0,(visible.size()-1)/rows));listX=20;listWidth=width>=540?(width-60)/2:width-40;contextX=listWidth+40;contextWidth=width-contextX-20;
  int start=page*rows;for(int i=start;i<Math.min(visible.size(),start+rows);i++){JsonObject m=visible.get(i);String id=m.get("id").getAsString();int bid=100+i;GuiButton b=new FlatButton(bid,listX,86+(i-start)*25,listWidth,22,m.get("name").getAsString()+"  "+(Config.foundations.contains(id)?(Core.available(id)?"FOUNDATION ACTIVE":"NOT INSTALLED"):!Core.available(id)?"PROVIDER MISSING":Config.module(id).get("enabled").getAsBoolean()?"ON":"OFF"));buttonList.add(b);selectIds.put(bid,id);}
  buttonList.add(new FlatButton(4,20,height-40,70,22,"Previous"));buttonList.add(new FlatButton(5,100,height-40,60,22,"Next"));
  buttonList.add(new FlatButton(9,20,height-70,140,22,"HUD Editor"));
  if(width<540){buttonList.add(new FlatButton(6,170,height-40,Math.max(80,width-190),22,"Edit selected"));return;}
  contextButtons();
 }
 private void contextButtons(){JsonObject meta=meta();if(meta==null)return;int y=111;String status=meta.get("status").getAsString();GuiButton b=new FlatButton(7,contextX,y,contextWidth,22,(Config.foundations.contains(selected)?(Core.available(selected)?"Foundation active":"Provider missing"):Config.module(selected).get("enabled").getAsBoolean()?"Disable":"Enable")+" "+meta.get("name").getAsString());b.enabled=!status.equals("candidate")&&Core.canToggle(selected);buttonList.add(b);y+=30;
  int bid=2000;for(Map.Entry<String,JsonElement> p:meta.getAsJsonObject("settings").entrySet()){GuiButton param=new FlatButton(bid,contextX,y,contextWidth,22,settingText(p.getKey(),Config.module(selected).get(p.getKey())));buttonList.add(param);paramIds.put(bid++,p.getKey());y+=25;}
  if(selected.equals("hypixel")){buttonList.add(new FlatButton(8,contextX,y,contextWidth,22,"Request Party Info"));}
 }
 private JsonObject meta(){for(JsonElement e:Config.catalog)if(e.getAsJsonObject().get("id").getAsString().equals(selected))return e.getAsJsonObject();return null;}
 @Override public void drawScreen(int x,int y,float partial){drawRect(0,0,width,height,0xfff5f4f0);drawRect(0,0,width,44,0xffffffff);fontRendererObj.drawString(Config.brand,width/2-fontRendererObj.getStringWidth(Config.brand)/2,25,0xff343c45);drawRect(18,51,width-18,75,0xffc6cdd2);drawRect(20,53,width-20,73,0xffffffff);search.drawTextBox();if(search.getText().isEmpty()&&!search.isFocused())fontRendererObj.drawString("Search modules",25,59,0xff626971);
  if(visible.isEmpty())fontRendererObj.drawString("No matching modules. Try a shorter search.",20,90,0xff535e6b);
  if(width>=540){JsonObject m=meta();if(m!=null){fontRendererObj.drawString(m.get("name").getAsString(),contextX,87,0xff343c45);fontRendererObj.drawSplitString(m.get("status").getAsString()+" | "+(Core.available(selected)?Safety.reason(selected):"PolyPatcher missing. Install from Launcher Library."),contextX,Math.min(height-82,255),contextWidth,0xff5c6570);}}
  if(!Core.message.isEmpty())fontRendererObj.drawSplitString(Core.message,170,height-70,width-190,0xff824800);super.drawScreen(x,y,partial);
  if(!buttonList.isEmpty()&&focus>=0&&focus<buttonList.size()){GuiButton b=buttonList.get(focus);drawRect(b.xPosition-2,b.yPosition-2,b.xPosition+b.width+2,b.yPosition,0xff365fd0);}
 }
 @Override protected void actionPerformed(GuiButton b)throws IOException{
  if(b.id==1){mc.displayGuiScreen(parent);return;}if(b.id==3){if(Config.save())Core.message="Saved";return;}
  if(b.id==2){List<String> groups=new ArrayList<String>();for(JsonElement e:Config.catalog){String g=e.getAsJsonObject().get("group").getAsString();if(!groups.contains(g))groups.add(g);}group=groups.get((groups.indexOf(group)+1)%groups.size());page=0;search.setText("");selected="";for(JsonElement e:Config.catalog)if(e.getAsJsonObject().get("group").getAsString().equals(group)){selected=e.getAsJsonObject().get("id").getAsString();break;}build();return;}
  if(b.id==4){page=Math.max(0,page-1);build();return;}if(b.id==5){page++;build();return;}
  if(selectIds.containsKey(b.id)){selected=selectIds.get(b.id);build();return;}
  if(b.id==6){mc.displayGuiScreen(new ModuleScreen(this,selected));return;}
  if(b.id==7){JsonObject m=Config.module(selected);m.addProperty("enabled",!m.get("enabled").getAsBoolean());Config.save();build();return;}
  if(b.id==8){HypixelBridge.requestParty();return;}
  if(b.id==9){mc.displayGuiScreen(new HudEditorScreen(this));return;}
  if(paramIds.containsKey(b.id)){cycle(selected,paramIds.get(b.id));build();}
 }
 static String settingText(String key,JsonElement value){String label=key;String[] keys={"rightDrag","leftCollect","shiftDrag","wheel","scrollDirection","searchDirection","threeD","combatFocus","adaptive","window","target","factor","height","opacity","party","layout"};String[] names={"Right-drag placement","Left-drag collect","Shift-drag transfer","Scroll transfer","Scroll direction","Search direction","Optional 3D","Combat focus","Adaptive display","Sample window","Target color","Zoom factor","Fire height","Fire opacity","Party information","Keyboard layout (Linux)"};for(int i=0;i<keys.length;i++)if(keys[i].equals(key)){label=names[i];break;}return label+": "+(value.getAsJsonPrimitive().isBoolean()?(value.getAsBoolean()?"ON":"OFF"):value.getAsString());}
 static void cycle(String selected,String key){JsonObject def=null;for(JsonElement e:Config.catalog)if(e.getAsJsonObject().get("id").getAsString().equals(selected))def=e.getAsJsonObject().getAsJsonObject("settings").getAsJsonObject(key);if(def==null)return;JsonObject m=Config.module(selected);String t=def.get("type").getAsString();if(t.equals("boolean"))m.addProperty(key,!m.get(key).getAsBoolean());else if(t.equals("select")){JsonArray options=def.getAsJsonArray("options");int i=0;for(int j=0;j<options.size();j++)if(options.get(j).getAsString().equals(m.get(key).getAsString()))i=j;m.add(key,options.get((i+1)%options.size()));}else{double value=m.get(key).getAsDouble()+(key.equals("height")||key.equals("factor")?.1:1);if(value>def.get("max").getAsDouble())value=def.get("min").getAsDouble();m.addProperty(key,value);}Config.save();}
 @Override protected void mouseClicked(int x,int y,int b)throws IOException{search.mouseClicked(x,y,b);if(search.isFocused())focus=-1;super.mouseClicked(x,y,b);}
 @Override protected void keyTyped(char c,int key)throws IOException{if(key==Keyboard.KEY_ESCAPE){mc.displayGuiScreen(parent);return;}if(key==Keyboard.KEY_TAB){focus+=Keyboard.isKeyDown(Keyboard.KEY_LSHIFT)||Keyboard.isKeyDown(Keyboard.KEY_RSHIFT)?-1:1;if(focus>=buttonList.size())focus=-1;if(focus<-1)focus=buttonList.size()-1;search.setFocused(focus==-1);return;}if(key==Keyboard.KEY_RETURN&&!search.isFocused()){if(focus>=0&&focus<buttonList.size()&&buttonList.get(focus).enabled)actionPerformed(buttonList.get(focus));return;}if(search.textboxKeyTyped(c,key)){page=0;build();return;}super.keyTyped(c,key);}
 @Override public void handleMouseInput()throws IOException{super.handleMouseInput();int wheel=Mouse.getEventDWheel();if(wheel!=0){page=Math.max(0,page+(wheel<0?1:-1));build();}}
 @Override public boolean doesGuiPauseGame(){return false;}
 static class FlatButton extends GuiButton {
  FlatButton(int id,int x,int y,int w,int h,String text){super(id,x,y,w,h,text);}
  @Override public void drawButton(net.minecraft.client.Minecraft m,int x,int y){if(!visible)return;hovered=x>=xPosition&&y>=yPosition&&x<xPosition+width&&y<yPosition+height;int edge=enabled?hovered?0xff9c7426:0xffc6cdd2:0xffd9dddf;drawRect(xPosition,yPosition,xPosition+width,yPosition+height,edge);drawRect(xPosition+1,yPosition+1,xPosition+width-1,yPosition+height-1,enabled?hovered?0xfffff0cd:0xffffffff:0xffeeeeed);String text=m.fontRendererObj.trimStringToWidth(displayString,width-14);m.fontRendererObj.drawString(text,xPosition+7,yPosition+(height-8)/2,enabled?0xff343c45:0xff626971);}
 }
 private static class ModuleScreen extends GuiScreen {
  final GuiScreen parent;final String id;final Map<Integer,String> props=new HashMap<Integer,String>();int focus,page,rows,pages;
  ModuleScreen(GuiScreen p,String mid){parent=p;id=mid;}
  public void initGui(){buttonList.clear();props.clear();buttonList.add(new FlatButton(1,20,20,80,22,"Back"));JsonObject meta=null;for(JsonElement e:Config.catalog)if(e.getAsJsonObject().get("id").getAsString().equals(id))meta=e.getAsJsonObject();if(meta==null)return;GuiButton toggle=new FlatButton(2,20,65,width-40,22,Config.foundations.contains(id)?(Core.available(id)?"Foundation active":"Provider missing"):Config.module(id).get("enabled").getAsBoolean()?"Disable":"Enable");toggle.enabled=!meta.get("status").getAsString().equals("candidate")&&Core.canToggle(id);buttonList.add(toggle);rows=Math.max(1,(height-145)/26);pages=Math.max(1,(meta.getAsJsonObject("settings").entrySet().size()+rows-1)/rows);page=Math.min(page,pages-1);int y=95,bid=100,index=0;for(Map.Entry<String,JsonElement> e:meta.getAsJsonObject("settings").entrySet()){if(index>=page*rows&&index<(page+1)*rows){buttonList.add(new FlatButton(bid,20,y,width-40,22,settingText(e.getKey(),Config.module(id).get(e.getKey()))));props.put(bid++,e.getKey());y+=26;}index++;}if(pages>1){buttonList.add(new FlatButton(3,20,height-38,80,22,"Previous"));buttonList.add(new FlatButton(4,110,height-38,70,22,"Next"));}focus=Math.min(focus,buttonList.size()-1);}
  public void drawScreen(int x,int y,float pt){drawRect(0,0,width,height,0xfff5f4f0);fontRendererObj.drawString(fontRendererObj.trimStringToWidth(Config.brand+" / "+id,width-130),110,27,0xff343c45);super.drawScreen(x,y,pt);if(pages>1)fontRendererObj.drawString("Page "+(page+1)+" / "+pages,190,height-31,0xff343c45);if(focus>=0&&focus<buttonList.size()){GuiButton b=buttonList.get(focus);drawRect(b.xPosition-2,b.yPosition-2,b.xPosition+b.width+2,b.yPosition,0xff365fd0);}}
  protected void actionPerformed(GuiButton b){if(b.id==1){mc.displayGuiScreen(parent);return;}if(b.id==2){Config.module(id).addProperty("enabled",!Config.module(id).get("enabled").getAsBoolean());Config.save();}else if(b.id==3)page=Math.max(0,page-1);else if(b.id==4)page=Math.min(pages-1,page+1);else if(props.containsKey(b.id))cycle(id,props.get(b.id));initGui();}
  protected void keyTyped(char c,int key)throws IOException{if(key==Keyboard.KEY_ESCAPE){mc.displayGuiScreen(parent);return;}if(key==Keyboard.KEY_TAB){focus=(focus+(Keyboard.isKeyDown(Keyboard.KEY_LSHIFT)||Keyboard.isKeyDown(Keyboard.KEY_RSHIFT)?buttonList.size()-1:1))%buttonList.size();return;}if(key==Keyboard.KEY_RETURN&&buttonList.get(focus).enabled)actionPerformed(buttonList.get(focus));}
  public void handleMouseInput()throws IOException{super.handleMouseInput();int wheel=Mouse.getEventDWheel();if(wheel!=0){page=Math.max(0,Math.min(pages-1,page+(wheel<0?1:-1)));initGui();}}
  public boolean doesGuiPauseGame(){return false;}
 }
}
