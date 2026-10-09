package dev.ttro;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.renderer.GlStateManager;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraftforge.client.event.RenderLivingEvent;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
/** Only the normal server-provided display name. Never requests or invents levels. */
public final class Identity {
 @SubscribeEvent public void name(RenderLivingEvent.Specials.Pre e){
  Minecraft m=Minecraft.getMinecraft();if(!(e.entity instanceof EntityPlayer)||m.thePlayer==null||e.entity==m.getRenderViewEntity()||e.entity.isInvisibleToPlayer(m.thePlayer)||e.entity.isSneaking())return;
  if(e.entity.getTeam()!=null&&e.entity.getTeam().getNameTagVisibility()!=net.minecraft.scoreboard.Team.EnumVisible.ALWAYS)return;
  // Keep the original renderer whenever a server has an additional below-name objective.
  if(e.entity.worldObj.getScoreboard().getObjectiveInDisplaySlot(2)!=null||e.entity.getDistanceSqToEntity(m.thePlayer)>4096)return;
  String name=e.entity.getDisplayName().getFormattedText();int w=m.fontRendererObj.getStringWidth(name);GlStateManager.pushMatrix();
  try{GlStateManager.translate(e.x,e.y+e.entity.height+.4,e.z);GlStateManager.rotate(-m.getRenderManager().playerViewY,0,1,0);GlStateManager.rotate(m.getRenderManager().playerViewX,1,0,0);GlStateManager.scale(-.025,-.025,.025);GlStateManager.disableLighting();GlStateManager.enableBlend();GlStateManager.enableDepth();Gui.drawRect(-w/2-5,-3,w/2+5,11,0xc02a3039);m.fontRendererObj.drawString(name,-w/2,0,0xffffffff);e.setCanceled(true);
  }finally{GlStateManager.enableLighting();GlStateManager.disableBlend();GlStateManager.color(1,1,1,1);GlStateManager.popMatrix();}
 }
}
