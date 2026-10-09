package dev.ttro;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.renderer.GlStateManager;
import net.minecraft.init.Items;
import net.minecraft.item.ItemStack;
/** Runs only for rendered player skulls. No inventory scan, skin requests, or animation. */
public final class HeadFx {
 private static boolean pushed;
 public static void begin(ItemStack s,int x,int y){
  pushed=false;
  if(!Core.headFxEnabled||s==null||s.getItem()!=Items.skull||s.getMetadata()!=3||!Config.bool("headfx","threeD"))return;
  GlStateManager.pushMatrix();GlStateManager.translate(x+8,y+8,0);GlStateManager.rotate(-9,0,0,1);GlStateManager.scale(1.10f,1.10f,1);GlStateManager.translate(-x-8,-y-8,0);pushed=true;
 }
 public static void end(ItemStack s,int x,int y){
  if(pushed){GlStateManager.popMatrix();pushed=false;}
  if(!Core.headFxEnabled||s==null||s.getItem()!=Items.skull||s.getMetadata()!=3)return;
  GlStateManager.pushMatrix();GlStateManager.translate(0,0,210);GlStateManager.disableDepth();
  OverlayBatch.begin();try{String p=Config.text("headfx","preset");int c=p.equals("Clean")?0xffd5dde2:0xffe8b63e;
   if(p.equals("Glow")){OverlayBatch.rect(x-2,y-2,x+18,y+18,0x22f1b631);OverlayBatch.rect(x-1,y-1,x+17,y+17,0x33f1b631);}
   OverlayBatch.rect(x,y+15,x+16,y+17,c);
   if(!p.equals("Clean")){OverlayBatch.rect(x,y,x+3,y+1,c);OverlayBatch.rect(x,y,x+1,y+3,c);OverlayBatch.rect(x+13,y,x+16,y+1,c);OverlayBatch.rect(x+15,y,x+16,y+3,c);}
  }finally{OverlayBatch.end();GlStateManager.enableDepth();GlStateManager.enableAlpha();GlStateManager.color(1,1,1,1);GlStateManager.popMatrix();}
 }
 private HeadFx(){}
}
