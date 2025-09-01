"""
ASCII art for hoho terminal interface
"""

# The official Saitama "OK" face - based on banner art
OK_FACE_SAITAMA = """
                           @@@@@@@@@@@@@@@@@                        
                          %@@@@@@@@@@@@@@@@@@@@@                    
                    =@@@@@@@                @@@@@@                  
                   @@@@@@@@*                   +@@@                 
                 @@@@                             @@@               
                #@@                                @@@              
               @@@                                  =@@             
               @*                                    @@%            
                 -+:                                  @@            
             @@@@##-          -               @@       @@           
             @@              @@@@@@@@@@      #@@@@@@@# @@           
             @@@@          @@ @       @@     #@      @ @@           
             @  @@@        @  @ @@@@- =-  =+   @@@@  . @@           
           +@@ .@@@        @@% @    = @:  @:*@ *  =  @ @@           
           @     @           @@@.  +@@@   @ @ @@  @@@  @@           
           @ @@.@@                        @ @          @@           
           @@  @ @@@@                      @ @         @@           
           @@@@@ :@ -#                     @ @         @@           
            #  @@@@ +-                      :          @@           
            .@+                         @@= @@        @@*           
              #@@@@@@                   @ @* @        @@            
                    @@                  @@%=@@       @@             
                     @@.                            @@              
                      @@@@@%                      @@@               
                       @@@@@@                   @@@@                
                           *@@@@:            @@@@@                  
                            *@@@@@@@@@@@@@@@@@@@                    
                                @@@@@@@@@@@@@                       
"""

# Medium "OK" face for status displays
OK_FACE_MEDIUM = """
        ████████████████
    ██████████████████████
  ██████████████████████████
 ████████████████████████████
████████████████████████████
████████████████████████████
████████████████████████████
████████████████████████████
████  ████    ████  ████
████  ████    ████  ████
████  ████    ████  ████
████████████████████████████
████████████████████████████
████████████████████████████
████████  ██  ████████
████████  ██  ████████
████████████████████████████
████████████████████████████
████████████████████████████
 ████████████████████████████
  ██████████████████████████
    ██████████████████████
        ████████████████
"""

# Small "OK" face for inline use
OK_FACE_SMALL = """
    ████████████
  ████████████████
 ██████████████████
██████████████████
██████████████████
███ ███  ███ ███
███ ███  ███ ███
██████████████████
██████████████████
████ ██ ████
████ ██ ████
██████████████████
██████████████████
 ██████████████████
  ████████████████
    ████████████
"""

# Minimal "OK" face for compact displays
OK_FACE_MINI = """
  ████████
 ██████████
██████████
██ ██  ██ ██
████████████
██ ██ ██
████████████
██████████
 ██████████
  ████████
"""

# Text-based "OK" face (for when ASCII blocks don't render well)
OK_FACE_TEXT = """
     ooooooooo
   ooooooooooooo
  ooooooooooooooo
 ooooooooooooooooo
ooooooooooooooooooo
ooooooooooooooooooo
oo ooo   ooo oo
oo ooo   ooo oo
ooooooooooooooooooo
ooooooooooooooooooo
ooooo o ooooo
ooooo o ooooo
ooooooooooooooooooo
ooooooooooooooooooo
 ooooooooooooooooo
  ooooooooooooooo
   ooooooooooooo
     ooooooooo
"""

# Simple dot-matrix style
OK_FACE_DOTS = """
    ● ● ● ● ● ● ● ●
  ● ● ● ● ● ● ● ● ● ●
● ● ● ● ● ● ● ● ● ● ● ●
● ● ● ● ● ● ● ● ● ● ● ●
● ●   ● ●   ● ●   ● ●
● ●   ● ●   ● ●   ● ●
● ● ● ● ● ● ● ● ● ● ● ●
● ● ● ● ● ● ● ● ● ● ● ●
● ● ●   ●   ● ● ●
● ● ●   ●   ● ● ●
● ● ● ● ● ● ● ● ● ● ● ●
● ● ● ● ● ● ● ● ● ● ● ●
  ● ● ● ● ● ● ● ● ● ●
    ● ● ● ● ● ● ● ●
"""

def get_ok_face(size="saitama"):
    """Get the appropriate OK face ASCII art by size"""
    faces = {
        "saitama": OK_FACE_SAITAMA,  # The official one
        "large": OK_FACE_LARGE,
        "medium": OK_FACE_MEDIUM, 
        "small": OK_FACE_SMALL,
        "mini": OK_FACE_MINI,
        "text": OK_FACE_TEXT,
        "dots": OK_FACE_DOTS
    }
    return faces.get(size, OK_FACE_SAITAMA)  # Default to the real Saitama