/**
* <author>Christophe Roblin</author>
* <email>lifxmod@gmail.com</email>
* <url>lifxmod.com</url>
* <credits></credits>
* <description>Disconnects user on preConnect if server is full</description>
* <license>GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007</license>
*
* Modified by Spencer10798 - Github.com/Spencer10798
* January 11th, 2024
* Repurposed to serve as an automatic AFK kicker.
*/

if (!isObject(LiFxFullServerFixVIPVIP))
{
    new ScriptObject(LiFxFullServerFixVIP)
    {
    };
}
if(!isObject($LiFx::FullServerFixIdleTimeout))
  $LiFx::FullServerFixIdleTimeout = 60;

package LiFxFullServerFixVIP
{
  function LiFxFullServerFixVIP::setup() {
    LiFx::registerCallback($LiFx::hooks::onPostConnectRoutineCallbacks, onPostConnectRequest, LiFxFullServerFixVIP);
    LiFx::registerCallback($LiFx::hooks::onInitServerDBChangesCallbacks, dbInit, LiFxFullServerFixVIP);
    LiFx::registerCallback($LiFx::hooks::onConnectCallbacks,onConnectClient, LiFxFullServerFixVIP);
    LiFx::registerCallback($LiFx::hooks::onTick, onProcessTick, LiFxFullServerFixVIP);
  }
  
  function LiFxFullServerFixVIP::dbInit() {
    dbi.Update("ALTER TABLE `character` ADD COLUMN `LastUpdated` TIMESTAMP NULL DEFAULT NULL AFTER `DeleteTimestamp`");
    dbi.Update("DROP TRIGGER IF EXISTS `character_before_update`;");
    %character_before_update = "CREATE TRIGGER `character_before_update` BEFORE UPDATE ON `character`; FOR EACH ROW BEGIN\n";
    %character_before_update = %character_before_update @ "IF(NEW.GeoID != OLD.GeoID OR NEW.GeoAlt != OLD.GeoAlt) THEN\n";
    %character_before_update = %character_before_update @ "SET NEW.LastUpdated = CURRENT_TIMESTAMP;\n";
    %character_before_update = %character_before_update @ "END IF;\n";
    %character_before_update = %character_before_update @ "END\n";
    dbi.Update(%character_before_update);
  }
  function LiFxFullServerFixVIP::version() {
    return "v0.1.AFK";
  }

  function LiFxFullServerFixVIP::onProcessTick(%this, %client) {
    dbi.Select(LiFxFullServerFixVIP, "AFKKick", "SELECT c.ID AS ClientId, lc.active as Active FROM `lifx_character` lc LEFT JOIN `character` c ON c.ID = lc.id WHERE TIMESTAMPDIFF(MINUTE,c.LastUpdated,CURRENT_TIMESTAMP) > " @ $LiFx::FullServerFixIdleTimeout @ " ORDER BY lc.active DESC, TIMESTAMPDIFF(MINUTE,c.LastUpdated,CURRENT_TIMESTAMP) DESC LIMIT 1");

  }

  function LiFxFullServerFixVIP::onConnectClient(%this, %client) {
    dbi.Update("UPDATE `character` SET LastUpdated = now() WHERE id=" @ %client.getCharacterId());
  }
  function LiFxFullServerFixVIP::onPostConnectRequest(%this, %client, %nettAddress, %name) {
    %client.ConnectedTime = getUnixTime();
    if ($Server::PlayerCount > $Server::MaxPlayers)
    {
        LiFxFullServerFixVIP.ConReq = new ScriptObject() {
          Client = %client;
          NettAddress = %nettAddress;
          Name = %name;
        };
        %client.ConnectedTime = getUnixTime();
        
    }
    dbi.Update("UPDATE `character` SET LastUpdated = now() WHERE AccountID=" @ %client.getAccountId());
  }

  function LiFxFullServerFixVIP::AFKKick(%this,%rs) {
    if(%rs.ok() && %rs.nextRecord())
    {
      %ClientID = %rs.getFieldValue("ClientID");
      %Active = %rs.getFieldValue("Active");
      %ClientVIP = %rs.getFieldValue("ClientVIP");

      for(%id = 0; %id < ClientGroup.getCount(); %id++)
      {
        %client = ClientGroup.getObject(%id);
        if(%client.ConnectedTime <= (getUnixTime() - 60) && !isObject(%client.Player) && %client != LiFxFullServerFixVIP.ConReq.client && !%ClientVIP)
        {
          %client.scheduleDelete("You have been ejected from the server due to inactivity (AFK)", 100);
          break;
        } 
      }
      if(ClientGroup.getCount() == %id) {
        %this.ConReq.Client.scheduleDelete("Server is full without idlers, try again in 5 mins", 100);
      }
      
    }
    else {   
      warn("Connection from" SPC %this.ConReq.NetAddress SPC "(" @ %this.ConReq.Name @ ")" SPC "dropped due to CR_SERVERFULL");
      %this.ConReq.Client.scheduleDelete("Server is full", 100);
    }
    dbi.remove(%rs);
    %rs.delete();
  }
};
activatePackage(LiFxFullServerFixVIP);
LiFx::registerCallback($LiFx::hooks::mods, setup, LiFxFullServerFixVIP);