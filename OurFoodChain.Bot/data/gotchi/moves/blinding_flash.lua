﻿function OnRegister(move)

	move.SetName("blinding flash")
	move.SetDescription("Flashes bright lights, causing the opponent's next move to miss.")

	move.SetPP(5)
	move.SetPriority(2)

	move.Requires.DescriptionMatch("flash|bright light")

end

function OnMove(args)
	
	args.Target.SetStatus("blinded")
	args.SetText("emitting a blinding bright light")

end