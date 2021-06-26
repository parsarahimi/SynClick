# SynClick

Stands for synchronized click.

Can synchronize the click on two or more computers, one or more people can command the server to send a click command to everyone connected as client.

Uses an unsecure TCP server, can use it to chat and do the synchronized clicks. Does a ping test at the start to give you an idea of what your ping is. It uses a busy timer for the sleeping part for maximum accuracy ~1ms precision. Clients can set their own total half-ping, which is how long it takes for information to travel from server to client and from client to another target server like in the game. To work out the half-ping just add both pings together and divide by two.

![image](https://user-images.githubusercontent.com/33573025/123511839-c0dd9280-d67b-11eb-8179-fd09939363e8.png)
