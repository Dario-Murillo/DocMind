import { Component } from '@angular/core';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Query } from '../query/query';
import { Upload } from '../upload/upload';

@Component({
  imports: [MatSidenavModule, Query, Upload],
  selector: 'app-workspace',
  styleUrl: './workspace.css',
  templateUrl: './workspace.html',
})
export class Workspace {}
