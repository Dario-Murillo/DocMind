import { Component } from '@angular/core';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Conversations } from '../../core/conversations';
import { ConversationList } from '../conversation-list/conversation-list';
import { Query } from '../query/query';
import { Upload } from '../upload/upload';

@Component({
  imports: [ConversationList, MatSidenavModule, Query, Upload],
  providers: [Conversations],
  selector: 'app-workspace',
  styleUrl: './workspace.css',
  templateUrl: './workspace.html',
})
export class Workspace {}
